using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace Klever.VR.Updates
{
    [Serializable] public sealed class ReleaseKey { public string keyId, modulus, exponent; }
    [Serializable] public sealed class ReleaseConfiguration
    {
        public bool enabled, autoDownload;
        public string endpoint, channel;
        public string[] allowedHosts;
        public ReleaseKey[] keys;
    }
    public sealed class InstalledApp
    {
        public string Platform, PackageId, SigningCertificateSha256;
        public int VersionCode, AndroidApi;
    }
    public sealed class VerifiedRelease
    {
        public string Platform, Channel, PackageId, VersionName, ArtifactUrl, Sha256, SigningCertificateSha256, Notes;
        public string AcceleratedArtifactUrl;
        public int VersionCode, MinAndroidApi;
        public long SizeBytes, ExpiresAt;
    }
    public static class VrReleasePolicy
    {
        private static void Require(bool value, string message) { if (!value) throw new InvalidDataException(message); }
        private static string Text(JObject obj, string key)
        {
            Require(obj[key]?.Type == JTokenType.String, "Invalid release field: " + key);
            return (string)obj[key];
        }
        private static long Number(JObject obj, string key, long max = long.MaxValue)
        {
            Require(obj[key]?.Type == JTokenType.Integer, "Invalid release number: " + key);
            var value = (long)obj[key];
            Require(value > 0 && value <= max, "Release number outside range: " + key);
            return value;
        }
        public static bool IsHttps(string value, string[] approvedHosts)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == "https" &&
                string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Fragment) && uri.IsDefaultPort &&
                approvedHosts != null && Array.Exists(approvedHosts, host => string.Equals(host, uri.Host, StringComparison.OrdinalIgnoreCase));
        }
        public static VerifiedRelease Verify(string envelopeJson, ReleaseConfiguration config, InstalledApp app, long now)
        {
            Require(envelopeJson != null && Encoding.UTF8.GetByteCount(envelopeJson) <= 32768, "Release envelope exceeds limit.");
            var envelope = JObject.Parse(envelopeJson);
            var keyId = Text(envelope, "keyId");
            Require(Regex.IsMatch(keyId, "^[a-zA-Z0-9_-]{1,64}$"), "Invalid key ID.");
            var key = config.keys == null ? null : Array.Find(config.keys, candidate => candidate != null && candidate.keyId == keyId);
            Require(key != null, "Unknown release signing key.");
            var encoded = Text(envelope, "payload");
            var signatureText = Text(envelope, "signature");
            Require(encoded.Length <= 22000 && signatureText.Length <= 1500, "Invalid envelope encoding.");
            var bytes = Convert.FromBase64String(encoded);
            var signature = Convert.FromBase64String(signatureText);
            Require(bytes.Length <= 16384 && Convert.ToBase64String(bytes) == encoded && Convert.ToBase64String(signature) == signatureText, "Non-canonical envelope encoding.");
            var modulus = Convert.FromBase64String(key.modulus);
            Require(modulus.Length >= 256 && modulus.Length <= 512, "RSA release key must be 2048–4096 bits.");
            using (var rsa = RSA.Create())
            {
                rsa.ImportParameters(new RSAParameters { Modulus = modulus, Exponent = Convert.FromBase64String(key.exponent) });
                Require(rsa.VerifyData(bytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1), "Invalid release signature.");
            }
            var p = JObject.Parse(new UTF8Encoding(false, true).GetString(bytes));
            Require(Number(p, "schemaVersion") == 1, "Unsupported release schema.");
            var release = new VerifiedRelease {
                Platform = Text(p, "platform"), Channel = Text(p, "channel"), PackageId = Text(p, "packageId"),
                VersionCode = (int)Number(p, "versionCode", 2100000000), VersionName = Text(p, "versionName"),
                MinAndroidApi = (int)Number(p, "minAndroidApi", 100), SizeBytes = Number(p, "sizeBytes", 4L * 1024 * 1024 * 1024),
                ArtifactUrl = Text(p, "artifactUrl"), Sha256 = Text(p, "sha256"),
                SigningCertificateSha256 = Text(p, "signingCertificateSha256"), Notes = Text(p, "releaseNotes"),
                ExpiresAt = Number(p, "expiresAt")
            };
            Require(release.Platform == "quest3" || release.Platform == "dpvr-p1-pro-edu", "Invalid release platform.");
            Require(release.Channel == "uat" || release.Channel == "stable", "Invalid channel.");
            Require(release.Platform == app.Platform && release.PackageId == app.PackageId && release.Channel == config.channel, "Release targets a different app or channel.");
            Require(release.MinAndroidApi >= 23 && release.MinAndroidApi <= app.AndroidApi, "Android version is incompatible.");
            Require(release.VersionName.Length > 0 && release.VersionName.Length <= 100 && release.Notes.Length <= 2000, "Invalid release description.");
            Require(Regex.IsMatch(release.Sha256, "^[a-f0-9]{64}$") && Regex.IsMatch(release.SigningCertificateSha256, "^[a-f0-9]{64}$"), "Invalid release hash.");
            Require(string.Equals(release.SigningCertificateSha256, app.SigningCertificateSha256, StringComparison.OrdinalIgnoreCase), "Release signing identity differs from the installed app.");
            var issued = Number(p, "issuedAt");
            Require(issued <= now + 300 && release.ExpiresAt > now && release.ExpiresAt > issued && release.ExpiresAt - issued <= 30L * 86400, "Release expired or lifetime invalid.");
            Require(IsHttps(release.ArtifactUrl, config.allowedHosts), "Unapproved APK download host.");
            if (p["acceleratedArtifactUrl"] != null && p["acceleratedArtifactUrl"].Type != JTokenType.Null)
            {
                release.AcceleratedArtifactUrl = Text(p, "acceleratedArtifactUrl");
                Require(release.AcceleratedArtifactUrl.Length <= 2048 && IsHttps(release.AcceleratedArtifactUrl, config.allowedHosts), "Unapproved accelerated APK host.");
                var accelerated = new Uri(release.AcceleratedArtifactUrl);
                Require(accelerated.AbsolutePath == "/vr-releases/artifacts/" + release.Sha256 + ".apk" && string.IsNullOrEmpty(accelerated.Query), "Accelerated URL must identify the same immutable APK.");
            }
            return release;
        }
        public static string FileHash(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
    }
}
