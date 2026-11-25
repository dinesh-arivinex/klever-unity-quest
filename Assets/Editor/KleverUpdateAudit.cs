using System;
using System.IO;
using System.Reflection;
using Klever.VR.Updates;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Klever.VR.Editor
{
    public static class UpdateAudit
    {
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
        private static void Reject(Action action)
        {
            bool rejected = false;
            try { action(); } catch (Exception) { rejected = true; }
            Require(rejected, "Unsafe release was accepted.");
        }
        public static void Run()
        {
            AuditCacheRetention();
            var selectUrl = typeof(VrUpdateClient).GetMethod("SelectArtifactUrl", BindingFlags.Static | BindingFlags.NonPublic);
            var choice = new VerifiedRelease { ArtifactUrl = "https://origin.example/a.apk", Sha256 = new string('a', 64) };
            Require((string)selectUrl.Invoke(null, new object[] { choice, null }) == choice.ArtifactUrl, "Legacy origin was not retained.");
            choice.AcceleratedArtifactUrl = "https://cdn.example/a.apk";
            Require((string)selectUrl.Invoke(null, new object[] { choice, null }) == choice.AcceleratedArtifactUrl, "Approved acceleration was not preferred.");
            Require((string)selectUrl.Invoke(null, new object[] { choice, choice.Sha256 }) == choice.ArtifactUrl, "Origin fallback failed.");
            var rangePolicy = typeof(VrUpdateClient).GetMethod("ValidDownloadResponse", BindingFlags.Static | BindingFlags.NonPublic);
            Func<long, long, long, string, bool> acceptsRange = (offset, size, code, header) => (bool)rangePolicy.Invoke(null, new object[] { offset, size, code, header });
            Require(acceptsRange(0, 100, 200, null), "Fresh full-body response rejected.");
            Require(acceptsRange(40, 100, 206, "bytes 40-99/100"), "Exact retained-prefix response rejected.");
            Require(!acceptsRange(40, 100, 200, null), "Server ignoring Range accepted for append.");
            Require(!acceptsRange(40, 100, 206, "bytes 0-99/100"), "Wrong range start accepted.");
            Require(!acceptsRange(40, 100, 206, "bytes 40-98/100"), "Incomplete range end accepted.");
            Require(!acceptsRange(40, 100, 206, "bytes 40-99/101"), "Wrong artifact length accepted.");
            Require(!acceptsRange(40, 100, 206, null), "Missing range identity accepted.");
            Require(!acceptsRange(0, 100, 206, "bytes 0-99/100"), "Unexpected partial response accepted.");
            Require(!acceptsRange(100, 100, 416, "bytes */100"), "Complete retained file sent as range.");
            Debug.Log("[VR-DOWNLOAD-RANGE-AUDIT] PASS: exact resume range accepted; ignored, malformed and mismatched ranges rejected.");
            Require(AndroidBuildVersion.Validate("2", 1) == 2, "Valid Android update code rejected.");
            foreach (var invalid in new[] { "", "1", "0", "-1", "2.5", " 2", "+2", "2100000001" })
                Reject(() => AndroidBuildVersion.Validate(invalid, 1));
            var fixture = JObject.Parse(File.ReadAllText("Assets/Editor/Fixtures/VrReleaseSignatureFixture.json"));
            var config = new ReleaseConfiguration { channel = "uat", allowedHosts = new[] { "releases.example.com" }, keys = new[] { JsonConvert.DeserializeObject<ReleaseKey>(fixture["key"].ToString()) } };
            var app = new InstalledApp { Platform = "quest3", PackageId = "com.klever.vr.quest.paritytest", VersionCode = 1, AndroidApi = 33, SigningCertificateSha256 = new string('b', 64) };
            var envelope = fixture["envelope"].ToString();
            long now = (long)fixture["now"];
            var release = VrReleasePolicy.Verify(envelope, config, app, now);
            Require(release.VersionCode == 2 && release.SizeBytes == 100, "Node-signed release did not verify in Unity.");
            if (fixture["acceleratedEnvelope"] != null)
            {
                Reject(() => VrReleasePolicy.Verify(fixture["acceleratedEnvelope"].ToString(), config, app, now));
                config.allowedHosts = new[] { "releases.example.com", "cdn.example.com" };
                Require(VrReleasePolicy.Verify(fixture["acceleratedEnvelope"].ToString(), config, app, now).AcceleratedArtifactUrl.Contains("/vr-releases/artifacts/"), "Signed CDN URL did not verify.");
                Reject(() => VrReleasePolicy.Verify(fixture["badAcceleratedEnvelope"].ToString(), config, app, now));
                config.allowedHosts = new[] { "releases.example.com" };
            }
            var tampered = JObject.Parse(envelope);
            tampered["payload"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("{}"));
            Reject(() => VrReleasePolicy.Verify(tampered.ToString(), config, app, now));
            tampered = JObject.Parse(envelope); tampered["keyId"] = "untrusted";
            Reject(() => VrReleasePolicy.Verify(tampered.ToString(), config, app, now));
            Reject(() => VrReleasePolicy.Verify(envelope, config, app, now + 3600));
            Reject(() => VrReleasePolicy.Verify(envelope, config, app, now - 301));
            app.Platform = "dpvr-p1-pro-edu";
            Reject(() => VrReleasePolicy.Verify(envelope, config, app, now));
            app.Platform = "quest3"; app.PackageId = "com.other.app";
            Reject(() => VrReleasePolicy.Verify(envelope, config, app, now));
            app.PackageId = "com.klever.vr.quest.paritytest"; app.SigningCertificateSha256 = new string('c', 64);
            Reject(() => VrReleasePolicy.Verify(envelope, config, app, now));
            app.SigningCertificateSha256 = new string('b', 64); app.AndroidApi = 27;
            Reject(() => VrReleasePolicy.Verify(envelope, config, app, now));
            app.AndroidApi = 33; config.channel = "stable";
            Reject(() => VrReleasePolicy.Verify(envelope, config, app, now));
            config.channel = "uat"; config.allowedHosts = new[] { "other.example.com" };
            Reject(() => VrReleasePolicy.Verify(envelope, config, app, now));
            Require(!VrReleasePolicy.IsHttps("http://releases.example.com/a.apk", new[] { "releases.example.com" }), "HTTP download accepted.");
            Require(!VrReleasePolicy.IsHttps("https://user:password@releases.example.com/a.apk", new[] { "releases.example.com" }), "Credential URL accepted.");
            Require(!VrReleasePolicy.IsHttps("https://releases.example.com.attacker.test/a.apk", new[] { "releases.example.com" }), "Lookalike host accepted.");
            var shipped = Resources.Load<TextAsset>("KleverVrUpdates");
            if (shipped != null)
            {
                var approvedHash = Environment.GetEnvironmentVariable("KLEVER_UPDATE_CONFIG_SHA256");
                Require(!string.IsNullOrEmpty(approvedHash) && VrReleasePolicy.FileHash("Assets/Resources/KleverVrUpdates.json") == approvedHash.ToLowerInvariant(), "Update configuration must match an explicitly reviewed build hash.");
                var deployment = JsonUtility.FromJson<ReleaseConfiguration>(shipped.text);
                Require(deployment.enabled && deployment.autoDownload && deployment.channel == "uat" && deployment.endpoint == "https://api.klever-edu.com/vr-releases/check", "Only the reviewed pilot channel may be enabled in this UAT build.");
                Require(deployment.keys != null && deployment.keys.Length == 1 && deployment.keys[0].keyId == "dpvr-uat-20261110", "Unreviewed pilot key.");
                Require(deployment.allowedHosts != null && deployment.allowedHosts.Length == 2 && deployment.allowedHosts[0] == "api.klever-edu.com" && deployment.allowedHosts[1] == "klever-edu.com", "Unreviewed pilot artifact host.");
            }
            AuditInstallerGate();
#if KLEVER_DPN
            Klever.DPVR.Editor.DpvrUpdatePopupAudit.Run();
#endif
            Debug.Log("[VR-UPDATE-AUDIT] PASS: signature/identity rejection and optional deployed-resource review. Editor tests attempt no network or installation.");
        }
        private static void AuditCacheRetention()
        {
            var folder = Path.Combine(Path.GetTempPath(), "KleverUpdateRetention-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var oldApk = new string('a', 64) + ".apk";
            var names = new[] { "5-" + new string('c', 32) + ".partial", "5-" + new string('d', 32) + ".partial", oldApk, oldApk + ".manifest.json" };
            foreach (var name in names) File.WriteAllText(Path.Combine(folder, name), "retained " + name);
            var completed = Path.Combine(folder, new string('b', 64) + ".apk");
            var partial = Path.Combine(folder, new string('b', 64) + ".partial");
            var archive = VrUpdateCache.ArchiveInactive(folder, partial, completed);
            Require(archive != null && Directory.GetFiles(folder).Length == 0, "Legacy files still block a new download.");
            foreach (var name in names) Require(File.ReadAllText(Path.Combine(archive, name)) == "retained " + name, "Retention changed old updater bytes.");
            File.WriteAllText(partial, "current prefix"); File.WriteAllText(completed + ".manifest.json", "current metadata");
            File.WriteAllText(Path.Combine(folder, names[0]), "older prefix"); File.WriteAllText(Path.Combine(folder, "unknown.keep"), "support file");
            VrUpdateCache.ArchiveInactive(folder, partial, completed);
            Require(File.ReadAllText(partial) == "current prefix" && File.ReadAllText(completed + ".manifest.json") == "current metadata", "Active resume data was moved.");
            Require(File.ReadAllText(Path.Combine(folder, "unknown.keep")) == "support file", "Unknown files were touched.");
            Reject(() => VrUpdateCache.ArchiveInactive(folder, Path.Combine(Path.GetTempPath(), "outside.partial"), completed));
            Debug.Log("[VR-UPDATE-CACHE-AUDIT] PASS: old updater files retained; active prefix, metadata and unknown files preserved.");
        }
        private static void AuditInstallerGate()
        {
            int previousRunFlag = PlayerPrefs.GetInt("klever.telemetry.activeRun", 0);
            Klass.Net.LiveViewPublisher.SetExpectedUpdateExit(true);
            Require(PlayerPrefs.GetInt("klever.telemetry.activeRun", 1) == 0, "Planned package replacement is flagged as a crash.");
            Klass.Net.LiveViewPublisher.SetExpectedUpdateExit(false);
            Require(PlayerPrefs.GetInt("klever.telemetry.activeRun", 0) == 1, "Cancelled update did not restore active-run tracking.");
            var previous = VrUpdateClient.Instance;
            var go = new GameObject("Offline installer gate audit");
            var client = go.AddComponent<VrUpdateClient>();
            var instance = typeof(VrUpdateClient).GetProperty("Instance");
            var change = typeof(VrUpdateClient).GetMethod("Change", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                instance.SetValue(null, client);
                Require(!VrUpdateClient.DeferSessionControl(() => { }), "Inactive updater deferred lesson control.");
                change.Invoke(client, new object[] { VrUpdateClient.UpdateState.Installing });
                int replayed = 0;
                Require(VrUpdateClient.DeferSessionControl(() => replayed = 1), "Installer did not hold incoming lesson.");
                Require(VrUpdateClient.DeferSessionControl(() => replayed = 2), "Latest teacher command was not retained.");
                client.OnInstallerStatus("failed");
                Require(replayed == 2 && !client.InstallationActive, "Installer cancellation failed to replay only the latest teacher command.");
                client.OnInstallerStatus("awaiting_android");
                Require(!client.InstallationActive, "Late installer callback re-entered a completed attempt.");
                var notice = go.AddComponent<VrUpdateNotice>();
                typeof(VrUpdateNotice).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(notice, null);
                Require(go.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length == 2, "Update notice actions are missing.");
                var canvas = go.GetComponentInChildren<Canvas>(true);
                Require(canvas.GetComponent<UnityEngine.UI.Image>().raycastTarget, "Update dialog allows clicks through to hidden lobby keys.");
                foreach (var text in go.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true))
                {
                    Require(!text.richText, "Update text interprets release markup.");
                    Require(text.fontSharedMaterial != text.font.material, "Update overlay modifies the shared classroom font.");
                    Require(text.fontSharedMaterial.GetInt("unity_GUIZTestMode") == (int)UnityEngine.Rendering.CompareFunction.Always, "Update lettering can be hidden by classroom geometry.");
                }
                foreach (var button in go.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    Require(!string.IsNullOrWhiteSpace(button.GetComponentInChildren<TMPro.TextMeshProUGUI>().text), "Update action starts without a visible label.");
                var poseCamera = new GameObject("Pitched and rolled update pose audit");
                try
                {
                    var camera = poseCamera.AddComponent<Camera>();
                    camera.transform.SetPositionAndRotation(new Vector3(2, 1.6f, -3), Quaternion.Euler(65, 35, 40));
                    typeof(VrUpdateNotice).GetField("eye", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(notice, camera);
                    typeof(VrUpdateNotice).GetMethod("PlaceInFront", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(notice, null);
                    Require(Vector3.Dot(canvas.transform.up, Vector3.up) > 0.999f && Mathf.Abs(canvas.transform.position.y - camera.transform.position.y) < 0.001f, "Looking down or rolling the headset puts the update panel on the floor or tilts it.");
                    Require(Mathf.Abs(Vector3.Distance(canvas.transform.position, camera.transform.position) - 1.8f) < 0.001f, "Update panel distance is not comfortable.");
                }
                finally { UnityEngine.Object.DestroyImmediate(poseCamera); }
                PreviewNotice(go);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                instance.SetValue(null, previous);
                PlayerPrefs.SetInt("klever.telemetry.activeRun", previousRunFlag); PlayerPrefs.Save();
            }
            Debug.Log("[VR-INSTALLER-GATE-AUDIT] PASS: latest teacher command held/replayed on cancellation, late callback ignored, lobby UI built. No installer invoked.");
            var rulesDirectory = Path.Combine(Path.GetTempPath(), "klever-updater-rules-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(rulesDirectory);
            var rulesPath = Path.Combine(rulesDirectory, "proguard-unity.txt");
            File.WriteAllText(rulesPath, "# Existing SDK rules must survive\n");
            var buildHook = new UpdaterAndroidBuild();
            buildHook.OnPostGenerateGradleAndroidProject(rulesDirectory);
            var once = File.ReadAllText(rulesPath);
            buildHook.OnPostGenerateGradleAndroidProject(rulesDirectory);
            Require(File.ReadAllText(rulesPath) == once && once.Contains("Existing SDK rules must survive") && once.Contains("com.klever.updates.**"), "JNI keep-rule injection changed SDK rules or duplicated entries.");
        }
        private static void PreviewNotice(GameObject root)
        {
            var output = Environment.GetEnvironmentVariable("KLEVER_UPDATE_PREVIEW");
            if (string.IsNullOrEmpty(output)) return;
            var canvas = root.GetComponentInChildren<Canvas>(true);
            var texts = root.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
            foreach (var text in texts)
            {
                if (text.name == "Update title") text.text = "Klever update available";
                else if (text.name == "Update detail") text.text = "Update verified. Android may ask you to confirm installation. Your lessons and saved results will be kept.";
                else if (text.name.Contains("Update action")) text.text = "Install update";
            }
            canvas.gameObject.SetActive(true); canvas.transform.SetPositionAndRotation(new Vector3(0, 0, 1.8f), Quaternion.identity);
            var cameraObject = new GameObject("Update notice preview camera");
            var camera = cameraObject.AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.07f, 0.1f, 0.13f); camera.cullingMask = 1 << 5;
            var previousEventSystem = UnityEngine.EventSystems.EventSystem.current;
            var auditEventSystem = cameraObject.AddComponent<UnityEngine.EventSystems.EventSystem>();
            UnityEngine.EventSystems.EventSystem.current = auditEventSystem;
            canvas.worldCamera = camera;
            var target = new RenderTexture(1600, 900, 24); camera.targetTexture = target;
            // A closer opaque surface reproduces the reported runtime case:
            // the panel stays visible, so its lettering must stay visible too.
            var occluder = GameObject.CreatePrimitive(PrimitiveType.Quad);
            occluder.name = "Foreground geometry update visibility audit"; occluder.layer = 5;
            occluder.transform.SetPositionAndRotation(new Vector3(0, 0, 0.7f), Quaternion.identity);
            occluder.transform.localScale = new Vector3(4, 3, 1);
            var occluderMaterial = new Material(Shader.Find("Unlit/Color")); occluderMaterial.color = new Color(0.3f, 0.13f, 0.08f);
            occluder.GetComponent<Renderer>().sharedMaterial = occluderMaterial;
            var previous = RenderTexture.active;
            try
            {
                Canvas.ForceUpdateCanvases(); foreach (var text in texts) text.ForceMeshUpdate();
                camera.Render(); RenderTexture.active = target;
                var raycaster = canvas.GetComponent<UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster>();
                foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                {
                    var start = camera.transform.position + Vector3.down * 0.1f;
                    var direction = (button.transform.position - start).normalized;
                    var data = new UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceEventData(auditEventSystem) {
                        rayPoints = new System.Collections.Generic.List<Vector3> { start, start + direction * 5f },
                        layerMask = Physics.DefaultRaycastLayers
                    };
                    var hits = new System.Collections.Generic.List<UnityEngine.EventSystems.RaycastResult>();
                    raycaster.Raycast(data, hits);
                    var expected = button.GetComponent<UnityEngine.XR.Interaction.Toolkit.XRSimpleInteractable>() == null ? button.gameObject : canvas.gameObject;
                    Require(hits.Count > 0 && hits[0].gameObject == expected, "Controller graphic ray misses update modal: " + button.name);
                }
                Debug.Log("[VR-UPDATE-RAY-AUDIT] PASS: tracked-device rays are contained by the update modal at both action positions.");
                var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); pixels.Apply();
                File.WriteAllBytes(output, pixels.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(pixels);
            }
            finally
            {
                RenderTexture.active = previous; camera.targetTexture = null; target.Release();
                UnityEngine.Object.DestroyImmediate(target); UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.EventSystems.EventSystem.current = previousEventSystem;
                UnityEngine.Object.DestroyImmediate(occluder); UnityEngine.Object.DestroyImmediate(occluderMaterial);
            }
        }
    }
}
