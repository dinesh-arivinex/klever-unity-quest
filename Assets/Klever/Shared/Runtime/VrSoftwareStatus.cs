using System;
using UnityEngine;

namespace Klever.VR.Updates
{
    // Operational app metadata only. No lesson answers, tracking poses or credentials.
    public static class VrSoftwareStatus
    {
        [Serializable]
        private sealed class Report
        {
            public int schemaVersion = 1;
            public string platform, packageId, channel, versionName, signingCertificateSha256, updaterState;
            public int versionCode, offeredVersionCode, downloadProgress;
            public long reportedAt;
        }
        private static InstalledApp installed;
        private static string channel;
        private static float nextReport;
        private static string previousState;
        public static string GetIfDue()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                if (installed == null) installed = VrUpdateClient.ReadInstalledApp();
                if (channel == null)
                {
                    channel = "unconfigured";
                    var resource = Resources.Load<TextAsset>("KleverVrUpdates");
                    if (resource != null)
                    {
                        var config = JsonUtility.FromJson<ReleaseConfiguration>(resource.text);
                        if (config != null && config.enabled && (config.channel == "uat" || config.channel == "stable")) channel = config.channel;
                    }
                }
                var client = VrUpdateClient.Instance;
                var state = client != null ? client.State.ToString() : channel == "unconfigured" ? "Disabled" : "Unavailable";
                if (Time.realtimeSinceStartup < nextReport && state == previousState) return null;
                nextReport = Time.realtimeSinceStartup + 10f; previousState = state;
                return JsonUtility.ToJson(new Report {
                    platform = installed.Platform, packageId = installed.PackageId, versionCode = installed.VersionCode,
                    versionName = Application.version, signingCertificateSha256 = installed.SigningCertificateSha256,
                    channel = channel, updaterState = state, offeredVersionCode = client?.Release?.VersionCode ?? 0,
                    downloadProgress = client == null ? 0 : Mathf.Clamp(Mathf.RoundToInt(client.DownloadProgress * 100f), 0, 100),
                    reportedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
            }
            catch (Exception) { return null; } // A reporting failure must not interrupt a lesson.
#else
            return null;
#endif
        }
    }
}
