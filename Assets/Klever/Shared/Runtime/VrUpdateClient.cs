using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Networking;

namespace Klever.VR.Updates
{
    // Discovery and verified download only. An authorized installer must perform
    // the eventual package replacement; a verified download is never "installed".
    public sealed class VrUpdateClient : MonoBehaviour
    {
        public enum UpdateState { Disabled, Checking, UpToDate, Available, Downloading, VerifiedAwaitingInstaller, Unavailable, Verifying, PermissionRequired, Installing, AwaitingAndroid, Installed }
        public static VrUpdateClient Instance { get; private set; }
        public UpdateState State { get; private set; } = UpdateState.Disabled;
        public VerifiedRelease Release { get; private set; }
        public float DownloadProgress { get; private set; }
        public double DownloadBytesPerSecond { get; private set; }
        public long DownloadedBytes { get; private set; }
        public long SecondsRemaining => Release == null || DownloadBytesPerSecond < 1 ? -1 : (long)Math.Ceiling(Math.Max(0, Release.SizeBytes - DownloadedBytes) / DownloadBytesPerSecond);
        public string VerifiedApkPath { get; private set; }
        public event Action<UpdateState> StateChanged;
        private ReleaseConfiguration configuration;
        private InstalledApp installed;
        private string envelope;
        private string verifiedSha;
        private float deliveryCheckedAt = -10f;
        private bool deliveryPending;
        private float downloadLastProgressAt;
        private string failedAcceleratedSha;
        private float acceleratedRetryAfter;
        private Action deferredControl;
        public bool InstallationActive => State == UpdateState.Installing || State == UpdateState.AwaitingAndroid;
        public bool IsDeliveryPending
        {
            get
            {
                if (Time.realtimeSinceStartup - deliveryCheckedAt < 2f) return deliveryPending;
                deliveryCheckedAt = Time.realtimeSinceStartup;
                try
                {
                    var root = Path.Combine(Application.persistentDataPath, "learning-events");
                    if (!Directory.Exists(root)) return deliveryPending = false;
                    using (var files = Directory.EnumerateFiles(root, "*.pending*", SearchOption.AllDirectories).GetEnumerator()) return deliveryPending = files.MoveNext();
                }
                catch (Exception) { return deliveryPending = true; }
            }
        }
        public static bool DeferSessionControl(Action replay)
        {
            if (Instance == null || !Instance.InstallationActive) return false;
            Instance.deferredControl = replay;
            return true;
        }
        private void ReplayControl()
        {
            var replay = deferredControl; deferredControl = null;
            replay?.Invoke();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            RecordDiagnostic("bootstrap");
            // No shipped trust configuration means no checks, downloads or UI.
            var resource = Resources.Load<TextAsset>("KleverVrUpdates");
            if (resource == null) { RecordDiagnostic("configuration_absent"); return; }
            try
            {
                var config = JsonUtility.FromJson<ReleaseConfiguration>(resource.text);
                if (config == null || !config.enabled) return;
                RecordDiagnostic("reading_installed_identity");
                var app = ReadInstalledApp();
                RecordDiagnostic("identity_ready");
                if (!Uri.TryCreate(config.endpoint, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "https" ||
                    !endpoint.IsDefaultPort || !string.IsNullOrEmpty(endpoint.UserInfo) || !string.IsNullOrEmpty(endpoint.Query) || !string.IsNullOrEmpty(endpoint.Fragment) ||
                    (config.channel != "uat" && config.channel != "stable") || config.keys == null || config.keys.Length == 0 || config.allowedHosts == null || config.allowedHosts.Length == 0)
                    throw new InvalidDataException("Invalid update configuration.");
                var host = new GameObject("Klever verified app updates");
                DontDestroyOnLoad(host);
                var client = host.AddComponent<VrUpdateClient>();
                Instance = client; client.configuration = config; client.installed = app;
                RecordDiagnostic("creating_notice");
                host.AddComponent<VrUpdateNotice>();
                client.StartCoroutine(client.CheckLoop());
            }
            catch (Exception error) { RecordDiagnostic("bootstrap_failed", error); Debug.LogWarning("[VR-UPDATE] Update configuration unavailable; lesson operation continues."); }
        }
        // Local support status contains no learner identity, URLs, credentials or payloads.
        private static void RecordDiagnostic(string stage, Exception error = null)
        {
            try { File.WriteAllText(Path.Combine(Application.persistentDataPath, "update-status.txt"), DateTime.UtcNow.ToString("O") + " " + stage + (error == null ? "" : " " + error.GetType().Name)); }
            catch (Exception) { /* Diagnostics must never interrupt lessons. */ }
        }
        private void Change(UpdateState value) { State = value; RecordDiagnostic(value.ToString()); StateChanged?.Invoke(value); }
        private IEnumerator CheckLoop()
        {
            int failures = 0;
            while (true)
            {
                if (State == UpdateState.VerifiedAwaitingInstaller || State == UpdateState.PermissionRequired || InstallationActive) yield break;
                if (State == UpdateState.Downloading || State == UpdateState.Verifying || State == UpdateState.Checking) { yield return new WaitForSecondsRealtime(10f); continue; }
                yield return Check();
                if (State == UpdateState.VerifiedAwaitingInstaller) yield break;
                failures = State == UpdateState.Unavailable ? failures + 1 : 0;
                if (configuration.autoDownload && State == UpdateState.Available && !SafeToDownload())
                {
                    // A known update may be discovered during a lesson. Recheck its signed
                    // offer when the headset returns to the lobby, without waiting 15 minutes.
                    float recheckAt = Time.realtimeSinceStartup + 900f;
                    do { yield return new WaitForSecondsRealtime(3f); }
                    while (State == UpdateState.Available && !SafeToDownload() && Time.realtimeSinceStartup < recheckAt);
                    continue;
                }
                yield return new WaitForSecondsRealtime(failures > 0 ? Math.Min(300f, 15f * (float)Math.Pow(2, Math.Min(failures - 1, 5))) : 900f);
            }
        }
        private IEnumerator Check()
        {
            Change(UpdateState.Checking);
            var url = configuration.endpoint + "?platform=" + Uri.EscapeDataString(installed.Platform) + "&channel=" + Uri.EscapeDataString(configuration.channel) + "&packageId=" + Uri.EscapeDataString(installed.PackageId);
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 20; request.redirectLimit = 0;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (request.downloadedBytes > 32768) { request.Abort(); break; }
                    yield return null;
                }
                if (request.responseCode == 204) { Change(UpdateState.UpToDate); yield break; }
                if (request.result != UnityWebRequest.Result.Success || request.downloadedBytes > 32768) { Change(UpdateState.Unavailable); yield break; }
                try
                {
                    envelope = request.downloadHandler.text;
                    Release = VrReleasePolicy.Verify(envelope, configuration, installed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                }
                catch (Exception) { Change(UpdateState.Unavailable); yield break; }
            }
            MaintainUpdateCache();
            if (Release.VersionCode <= installed.VersionCode) { Change(UpdateState.UpToDate); yield break; }
            if (verifiedSha == Release.Sha256 && File.Exists(VerifiedApkPath)) { Change(UpdateState.VerifiedAwaitingInstaller); yield break; }
            Change(UpdateState.Available);
            if (configuration.autoDownload && SafeToDownload()) yield return Download();
        }
        public static bool SafeToDownload()
        {
            var sync = FindObjectOfType<DeviceSynHandler>();
            return sync != null && DeviceSynHandler.isOpenWelcomeScreen && string.IsNullOrEmpty(sync.experienceId) &&
                !sync.isOpenGame && !sync.isOpening && !sync.isSyncedDevice && !LessonPause.IsPaused && PlayerPrefs.GetInt("InSimulation", 0) == 0;
        }
        private void MaintainUpdateCache()
        {
            if (!SafeToDownload() || Release == null) return;
            try
            {
                var folder = Path.Combine(Application.persistentDataPath, "app-updates");
                if (Directory.Exists(folder)) VrUpdateCache.ArchiveInactive(folder,
                    Path.Combine(folder, Release.Sha256 + ".partial"), Path.Combine(folder, Release.Sha256 + ".apk"));
            }
            catch (Exception) { Debug.LogWarning("[VR-UPDATE] Retention maintenance deferred; the current app stays available."); }
        }
        private IEnumerator Download()
        {
            var task = DownloadCore();
            try
            {
                while (true)
                {
                    bool more;
                    try { more = task.MoveNext(); }
                    catch (Exception) { Change(UpdateState.Unavailable); yield break; }
                    if (!more) yield break;
                    yield return task.Current;
                }
            }
            finally { (task as IDisposable)?.Dispose(); }
        }
        private IEnumerator DownloadCore()
        {
            if (!SafeToDownload()) yield break;
            var folder = Path.Combine(Application.persistentDataPath, "app-updates");
            string partial;
            string completed;
            long retained;
            try
            {
                Directory.CreateDirectory(folder);
                // The signed artifact digest binds a retained prefix to this exact APK,
                // across retries/restarts. A complete file is always hashed again.
                completed = Path.Combine(folder, Release.Sha256 + ".apk");
                partial = File.Exists(completed) ? completed : Path.Combine(folder, Release.Sha256 + ".partial");
                retained = File.Exists(partial) ? new FileInfo(partial).Length : 0;
                VrUpdateCache.ArchiveInactive(folder, partial, completed);
                if (retained > Release.SizeBytes) { PreserveRejectedDownload(partial); throw new IOException("Invalid retained update length."); }
                RequireSpace(folder, Release.SizeBytes * 2 - retained + 100L * 1024 * 1024);
            }
            catch (Exception) { Change(UpdateState.Unavailable); yield break; }
            var downloadUrl = SelectArtifactUrl(Release, Time.realtimeSinceStartup < acceleratedRetryAfter ? failedAcceleratedSha : null);
            bool accelerated = downloadUrl != Release.ArtifactUrl;
            if (retained < Release.SizeBytes)
            {
                Change(UpdateState.Downloading);
                bool invalidResponse = false;
                bool acceleratorFault = false;
                bool success;
                using (var request = UnityWebRequest.Get(downloadUrl))
                {
                    request.downloadHandler = new DownloadHandlerFile(partial, retained > 0) { removeFileOnAbort = false };
                    if (retained > 0) request.SetRequestHeader("Range", "bytes=" + retained.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-");
                    request.redirectLimit = 0; request.timeout = 14400;
                    var operation = request.SendWebRequest();
                    ulong previousBytes = 0;
                    ulong sampledBytes = 0;
                    float sampledAt = Time.realtimeSinceStartup;
                    DownloadBytesPerSecond = 0; DownloadedBytes = retained;
                    downloadLastProgressAt = Time.realtimeSinceStartup;
                    while (!operation.isDone)
                    {
                        DownloadProgress = (float)((retained + (double)request.downloadedBytes) / Release.SizeBytes);
                        DownloadedBytes = retained + (long)request.downloadedBytes;
                        var elapsed = Time.realtimeSinceStartup - sampledAt;
                        if (elapsed >= 1f)
                        {
                            var rate = (request.downloadedBytes - sampledBytes) / elapsed;
                            DownloadBytesPerSecond = DownloadBytesPerSecond < 1 ? rate : DownloadBytesPerSecond * 0.6 + rate * 0.4;
                            sampledBytes = request.downloadedBytes; sampledAt = Time.realtimeSinceStartup;
                        }
                        if (request.downloadedBytes > previousBytes) { previousBytes = request.downloadedBytes; downloadLastProgressAt = Time.realtimeSinceStartup; }
                        if (request.responseCode >= 200 && !ValidDownloadResponse(retained, Release.SizeBytes, request.responseCode, request.GetResponseHeader("Content-Range")))
                        { invalidResponse = true; request.Abort(); break; }
                        if (!SafeToDownload() || request.downloadedBytes > (ulong)(Release.SizeBytes - retained) ||
                            Time.realtimeSinceStartup - downloadLastProgressAt > 120f || DateTimeOffset.UtcNow.ToUnixTimeSeconds() >= Release.ExpiresAt) { request.Abort(); break; }
                        yield return null;
                    }
                    if (request.responseCode >= 200 && !ValidDownloadResponse(retained, Release.SizeBytes, request.responseCode, request.GetResponseHeader("Content-Range"))) invalidResponse = true;
                    success = request.result == UnityWebRequest.Result.Success && !invalidResponse;
                    acceleratorFault = invalidResponse || request.responseCode >= 400 ||
                        (request.result == UnityWebRequest.Result.ConnectionError && SafeToDownload() && Application.internetReachability != NetworkReachability.NotReachable);
                }
                // A server that ignores Range could have appended a full body. Keep
                // those bytes as evidence, but never resume that damaged candidate.
                if (invalidResponse) PreserveRejectedDownload(partial);
                if (!success) { if (accelerated && acceleratorFault) { failedAcceleratedSha = Release.Sha256; acceleratedRetryAfter = Time.realtimeSinceStartup + 120f; } Change(UpdateState.Unavailable); yield break; }
            }
            // Hash incrementally to avoid freezing the headset on large APKs.
            Change(UpdateState.Verifying);
            bool verified = false;
            using (var stream = File.OpenRead(partial))
            using (var sha = SHA256.Create())
            {
                var buffer = new byte[1024 * 1024];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    sha.TransformBlock(buffer, 0, count, buffer, 0);
                    yield return null;
                }
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                verified = stream.Length == Release.SizeBytes &&
                    BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant() == Release.Sha256 &&
                    DateTimeOffset.UtcNow.ToUnixTimeSeconds() < Release.ExpiresAt;
            }
            if (!verified) { if (accelerated) { failedAcceleratedSha = Release.Sha256; acceleratedRetryAfter = Time.realtimeSinceStartup + 120f; } PreserveRejectedDownload(partial); Change(UpdateState.Unavailable); yield break; }
            // Retain all partial/failed downloads for support; never discard
            // learner files or replace the installed application here.
            VerifiedApkPath = completed;
            if (partial != completed) File.Move(partial, VerifiedApkPath);
            File.WriteAllText(VerifiedApkPath + ".manifest.json", envelope);
            DownloadProgress = 1;
            verifiedSha = Release.Sha256;
            Change(UpdateState.VerifiedAwaitingInstaller);
        }
        internal static bool ValidDownloadResponse(long retained, long size, long status, string contentRange)
        {
            if (retained < 0 || size <= 0 || retained >= size) return false;
            if (retained == 0) return status == 200 && string.IsNullOrEmpty(contentRange);
            return status == 206 && contentRange == string.Format(System.Globalization.CultureInfo.InvariantCulture, "bytes {0}-{1}/{2}", retained, size - 1, size);
        }
        internal static string SelectArtifactUrl(VerifiedRelease release, string failedAcceleratedSha)
        {
            return !string.IsNullOrEmpty(release.AcceleratedArtifactUrl) && failedAcceleratedSha != release.Sha256
                ? release.AcceleratedArtifactUrl : release.ArtifactUrl;
        }
        private static void PreserveRejectedDownload(string file)
        {
            if (File.Exists(file)) File.Move(file, file + "." + Guid.NewGuid().ToString("N") + ".rejected");
        }
        public void RetryCheck()
        {
            if (State == UpdateState.Unavailable && SafeToDownload()) StartCoroutine(Check());
        }
        public void RequestDownload()
        {
            if (State == UpdateState.Available && SafeToDownload()) StartCoroutine(Download());
        }
        public bool RequestFleetUpdate()
        {
            // A teacher request never interrupts a lesson or bypasses Android consent.
            if (!SafeToDownload()) return false;
            if (State == UpdateState.Available) RequestDownload();
            else if (State == UpdateState.UpToDate || State == UpdateState.Unavailable || State == UpdateState.Disabled)
                StartCoroutine(Check());
            return true;
        }
        public bool TryOpenInstallerAutomatically()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (configuration == null || !configuration.autoDownload ||
                State != UpdateState.VerifiedAwaitingInstaller || !SafeToDownload() || IsDeliveryPending)
                return false;
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass("com.klever.updates.KleverPackageInstaller"))
                    if (!bridge.CallStatic<bool>("isAllowed", activity)) return false;
            }
            catch (Exception) { return false; }
            RequestInstall();
            return InstallationActive;
#else
            return false;
#endif
        }

        public void RequestInstall()
        {
            deliveryCheckedAt = -10f;
            if ((State != UpdateState.VerifiedAwaitingInstaller && State != UpdateState.PermissionRequired) || !SafeToDownload() || IsDeliveryPending) return;
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var bridge = new AndroidJavaClass("com.klever.updates.KleverPackageInstaller"))
            {
                if (!bridge.CallStatic<bool>("isAllowed", activity))
                {
                    Change(UpdateState.PermissionRequired);
                    bridge.CallStatic("openPermission", activity);
                    return;
                }
            }
            }
            catch (Exception) { Change(UpdateState.Unavailable); return; }
            StartCoroutine(InstallVerified());
#endif
        }
        private IEnumerator InstallVerified()
        {
            // Lock incoming session-control handling before the final online
            // eligibility check. The latest teacher command is replayed if the
            // update is cancelled; a replaced app reconnects to retained state.
            Change(UpdateState.Installing);
            string currentEnvelope = null;
            var url = configuration.endpoint + "?platform=" + Uri.EscapeDataString(installed.Platform) + "&channel=" + Uri.EscapeDataString(configuration.channel) + "&packageId=" + Uri.EscapeDataString(installed.PackageId);
            using (var request = UnityWebRequest.Get(url))
            {
                request.timeout = 20; request.redirectLimit = 0;
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    if (request.downloadedBytes > 32768) { request.Abort(); break; }
                    yield return null;
                }
                if (request.result == UnityWebRequest.Result.Success && request.responseCode == 200 && request.downloadedBytes <= 32768) currentEnvelope = request.downloadHandler.text;
            }
            bool started = false;
            try
            {
                var fresh = VrReleasePolicy.Verify(currentEnvelope, configuration, installed, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
                deliveryCheckedAt = -10f;
                if (!SafeToDownload() || IsDeliveryPending || fresh.VersionCode != Release.VersionCode || fresh.Sha256 != Release.Sha256 ||
                    !File.Exists(VerifiedApkPath) || new FileInfo(VerifiedApkPath).Length != fresh.SizeBytes) throw new InvalidDataException("Update eligibility changed.");
#if UNITY_ANDROID && !UNITY_EDITOR
                Klass.Net.LiveViewPublisher.SetExpectedUpdateExit(true);
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var bridge = new AndroidJavaClass("com.klever.updates.KleverPackageInstaller"))
                    started = bridge.CallStatic<bool>("begin", activity, VerifiedApkPath, fresh.PackageId, fresh.VersionCode, fresh.SigningCertificateSha256, fresh.Sha256, fresh.SizeBytes);
#endif
            }
            catch (Exception) { started = false; }
            if (!started) { Klass.Net.LiveViewPublisher.SetExpectedUpdateExit(false); Change(UpdateState.Unavailable); ReplayControl(); }
        }
        [UnityEngine.Scripting.Preserve]
        public void OnInstallerStatus(string status)
        {
            if (!InstallationActive) return;
            if (status == "awaiting_android") { Change(UpdateState.AwaitingAndroid); return; }
            if (status == "installed")
            {
                try
                {
                    var actual = ReadInstalledApp();
                    if (Release != null && actual.VersionCode >= Release.VersionCode) { Change(UpdateState.Installed); deferredControl = null; return; }
                }
                catch (Exception) { }
            }
            Klass.Net.LiveViewPublisher.SetExpectedUpdateExit(false);
            Change(UpdateState.VerifiedAwaitingInstaller);
            ReplayControl();
        }
        private static void RequireSpace(string folder, long required)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var stats = new AndroidJavaObject("android.os.StatFs", folder))
                if (stats.Call<long>("getAvailableBytes") < required) throw new IOException("Insufficient update storage.");
#else
            if (new DriveInfo(Path.GetPathRoot(folder)).AvailableFreeSpace < required) throw new IOException("Insufficient update storage.");
#endif
        }
        public static InstalledApp ReadInstalledApp()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var manager = activity.Call<AndroidJavaObject>("getPackageManager"))
            using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
            {
                var api = version.GetStatic<int>("SDK_INT");
                var package = activity.Call<string>("getPackageName");
                using (var info = manager.Call<AndroidJavaObject>("getPackageInfo", package, api >= 28 ? 134217728 : 64))
                {
                    AndroidJavaObject[] signatures;
                    if (api >= 28)
                    {
                        using (var signing = info.Get<AndroidJavaObject>("signingInfo")) signatures = signing.Call<AndroidJavaObject[]>("getApkContentsSigners");
                    }
                    else signatures = info.Get<AndroidJavaObject[]>("signatures");
                    if (signatures == null || signatures.Length != 1) throw new InvalidDataException("Unsupported installed signing identity.");
                    string fingerprint;
                    try { using (var sha = SHA256.Create()) fingerprint = BitConverter.ToString(sha.ComputeHash(signatures[0].Call<byte[]>("toByteArray"))).Replace("-", "").ToLowerInvariant(); }
                    finally { foreach (var signature in signatures) signature?.Dispose(); }
                    return new InstalledApp {
#if KLEVER_DPN
                        Platform = "dpvr-p1-pro-edu",
#else
                        Platform = "quest3",
#endif
                        PackageId = package, AndroidApi = api, VersionCode = info.Get<int>("versionCode"), SigningCertificateSha256 = fingerprint
                    };
                }
            }
#else
            throw new PlatformNotSupportedException("Installed app update identity requires Android.");
#endif
        }
        private void OnApplicationPause(bool paused)
        {
            // Headset sleep must not look like two minutes of an awake, stalled connection.
            if (!paused)
            {
                downloadLastProgressAt = Time.realtimeSinceStartup;
                if (configuration != null && State == UpdateState.Unavailable && SafeToDownload()) StartCoroutine(Check());
            }
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
