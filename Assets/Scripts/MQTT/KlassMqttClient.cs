using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using SimpleJSON;
using uPLibrary.Networking.M2Mqtt;
using UnityEngine;

namespace Klass.Net
{
    /// <summary>
    /// Singleton MonoBehaviour wrapping M2Mqtt for the Klever_VR headset.
    ///
    /// Responsibilities:
    ///  - Fetch credentials (HTTP), connect to EMQX (TCP), publish LWT-aware status.
    ///  - Auto-reconnect with exponential backoff.
    ///  - Marshal incoming messages onto the Unity main thread so handlers can
    ///    safely touch GameObjects.
    ///  - Expose simple PublishState/PublishTelemetry/PublishCmdAck helpers and
    ///    OnSessionControl / OnCommand events.
    ///
    /// IMPORTANT: this script must live in a scene (or be instantiated with
    /// DontDestroyOnLoad) before any consumers call Instance.
    /// </summary>
    public class KlassMqttClient : MonoBehaviour
    {
        public static KlassMqttClient Instance { get; private set; }

        [Header("Tuning")]
        public float reconnectInitialSeconds = 2f;
        public float reconnectMaxSeconds = 30f;

        // ── Public state ───────────────────────────────────────────────────
        public bool IsConnected { get; private set; }
        public string SchoolId  { get; private set; }
        public string DeviceId  { get; private set; }

        // ── Public events (always fired on Unity main thread) ──────────────
        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action<JSONNode> OnSessionControl; // payload of session/control
        public event Action<JSONNode> OnCommand;        // payload of devices/{me}/cmd
        public event Action<JSONNode> OnBroadcast;      // payload of broadcast

        // ── Internals ──────────────────────────────────────────────────────
        private MqttClient _client;
        private LearningEventOutbox learningOutbox;
        private float nextOutboxSend;
        private MqttCredentialsFetcher.Credentials _creds;
        private readonly ConcurrentQueue<Action> _mainThread = new ConcurrentQueue<Action>();
        private readonly object _lock = new object();
        private float _backoff;
        private bool _wantConnected;
        private Coroutine _reconnectLoop;
        private Coroutine _refreshLoop;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Update()
        {
            if (IsConnected && learningOutbox != null && Time.realtimeSinceStartup >= nextOutboxSend)
            {
                nextOutboxSend = Time.realtimeSinceStartup + 0.5f;
                try { learningOutbox.SendPending(node => PublishJson(MqttTopics.Telemetry(SchoolId, DeviceId), node, false, 1)); }
                catch (Exception ex) { Debug.LogWarning("[Telemetry] Pending events retained: " + ex.Message); }
            }
            // Drain main-thread work queued from MQTT worker thread
            while (_mainThread.TryDequeue(out var act))
            {
                try { act(); } catch (Exception ex) { Debug.LogException(ex); }
            }
        }

        // A wear-sensor sleep can leave a half-open TCP connection. Reconnect
        // and resubscribe on wake to receive the latest retained teacher state.
        void OnApplicationPause(bool paused)
        {
            if (paused || !_wantConnected) return;
            Debug.Log("[KlassMqtt] Application resumed; reconnecting for retained session state.");
            if (_reconnectLoop != null) StopCoroutine(_reconnectLoop);
            if (_refreshLoop != null) { StopCoroutine(_refreshLoop); _refreshLoop = null; }
            SafeDisconnect();
            OnDisconnected?.Invoke();
            _backoff = reconnectInitialSeconds;
            _reconnectLoop = StartCoroutine(ReconnectLoop());
        }
        void OnApplicationQuit()
        {
            _wantConnected = false;
            DisconnectInternal("app quit");
        }

        // ── Public API ─────────────────────────────────────────────────────

        /// <summary>Begin connecting (idempotent). Will keep retrying.</summary>
        public void Begin()
        {
            if (_wantConnected) return;
            _wantConnected = true;
            _backoff = reconnectInitialSeconds;
            _reconnectLoop = StartCoroutine(ReconnectLoop());
        }

        public void End()
        {
            _wantConnected = false;
            if (_reconnectLoop != null) StopCoroutine(_reconnectLoop);
            if (_refreshLoop != null) StopCoroutine(_refreshLoop);
            DisconnectInternal("end");
        }

        public void PublishState(bool isActive, bool isSynced, bool isCompleted, string playState = null, string experienceId = null)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["isActive"]    = isActive;
            node["isSynced"]    = isSynced;
            node["isCompleted"] = isCompleted;
            if (!string.IsNullOrEmpty(playState))    node["playState"]    = playState;
            if (!string.IsNullOrEmpty(experienceId)) node["experienceId"] = experienceId;
            node["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            AppendSoftware(node);
            PublishJson(MqttTopics.State(SchoolId, DeviceId), node, retain: true, qos: 1);
        }

        /// <summary>
        /// Publish a richer state payload that includes the live asset-download
        /// progress (0-100). The teacher dashboard subscribes to retained state
        /// messages, so even a freshly-opened browser tab sees the latest value.
        /// </summary>
        public void PublishStateWithProgress(bool isActive, bool isSynced, bool isCompleted,
                                             int downloadProgress, string playState = null,
                                             string experienceId = null, bool handRaised = false)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["isActive"]         = isActive;
            node["isSynced"]         = isSynced;
            node["isCompleted"]      = isCompleted;
            node["downloadProgress"] = Mathf.Clamp(downloadProgress, 0, 100);
            if (!string.IsNullOrEmpty(playState))    node["playState"]    = playState;
            if (!string.IsNullOrEmpty(experienceId)) node["experienceId"] = experienceId;
            // Always include handRaised so it is preserved across retained-state
            // heartbeats and a refreshing teacher dashboard sees the latest value.
            node["handRaised"]   = handRaised;
            // Explicitly clear any previous downloadError when publishing normal state.
            node["downloadError"] = new JSONNull();
            node["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            AppendSoftware(node);
            PublishJson(MqttTopics.State(SchoolId, DeviceId), node, retain: true, qos: 1);
        }

        /// <summary>
        /// Publish a download-error state so the teacher dashboard can show
        /// a per-device error badge. The error is retained so page refreshes
        /// still see it until the device recovers or is removed.
        /// </summary>
        public void PublishDownloadError(string errorMessage)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["isActive"]         = true;
            node["isSynced"]         = false;
            node["isCompleted"]      = false;
            node["downloadProgress"] = 0;
            node["playState"]        = "idle";
            node["downloadError"]    = string.IsNullOrEmpty(errorMessage) ? "Unknown download error" : errorMessage;
            node["ts"]               = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            AppendSoftware(node);
            PublishJson(MqttTopics.State(SchoolId, DeviceId), node, retain: true, qos: 1);
        }

        private static void AppendSoftware(JSONObject node)
        {
            var report = Klever.VR.Updates.VrSoftwareStatus.GetIfDue();
            if (!string.IsNullOrEmpty(report)) node["software"] = JSON.Parse(report);
        }

        public void PublishTelemetry(JSONObject node)
        {
            if (node == null || string.IsNullOrEmpty(node["experienceId"].Value)) return;
            if (string.IsNullOrEmpty(SchoolId) || string.IsNullOrEmpty(DeviceId)) return;
            try
            {
                if (learningOutbox == null) learningOutbox = new LearningEventOutbox(SchoolId, DeviceId);
                learningOutbox.Add(node);
            }
            catch (Exception ex) { Debug.LogError("[Telemetry] Unable to persist event: " + ex.Message); }

        }

        public void PublishLiveViewState(JSONObject node)
        {
            if (!IsConnected) return;
            PublishJson(MqttTopics.LiveViewState(SchoolId, DeviceId), node, retain: true, qos: 1);
        }

        public void PublishLiveViewEvent(JSONObject node)
        {
            if (!IsConnected) return;
            PublishJson(MqttTopics.LiveViewEvents(SchoolId, DeviceId), node, retain: false, qos: 0);
        }

        public void PublishCmdAck(string msgId, bool ok, string error = null)
        {
            if (!IsConnected) return;
            var node = new JSONObject();
            node["msgId"] = msgId ?? "";
            node["ok"] = ok;
            if (!string.IsNullOrEmpty(error)) node["error"] = error;
            node["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
            PublishJson(MqttTopics.CmdAck(SchoolId, DeviceId), node, retain: false, qos: 1);
        }

        public void PublishJson(string topic, JSONNode body, bool retain, byte qos)
        {
            if (!IsConnected) return;
            try
            {
                var bytes = Encoding.UTF8.GetBytes(body == null ? "{}" : LearningEventOutbox.Normalize(body).ToString());
                _client.Publish(topic, bytes, qos, retain);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] publish failed on {topic}: {ex.Message}");
            }
        }

        // ── Connect + reconnect loop ───────────────────────────────────────

        private IEnumerator ReconnectLoop()
        {
            while (_wantConnected)
            {
                if (!IsConnected)
                {
                    yield return ConnectOnce();
                    if (!IsConnected)
                    {
                        var wait = _backoff;
                        _backoff = Mathf.Min(reconnectMaxSeconds, _backoff * 2f);
                        yield return new WaitForSecondsRealtime(wait);
                        continue;
                    }
                    _backoff = reconnectInitialSeconds;
                }
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        private IEnumerator ConnectOnce()
        {
            // 1. (re)fetch creds if missing or expired
            if (_creds == null || _creds.IsExpired)
            {
                bool done = false;
                bool ok = false;
                string err = null;
                yield return new MqttCredentialsFetcher().Fetch(
                    c => { _creds = c; ok = true; done = true; },
                    e => { err = e; done = true; });
                if (!done) yield break;
                if (!ok) { Debug.LogWarning($"[KlassMqtt] credential fetch failed: {err}"); yield break; }
                SchoolId = _creds.SchoolId;
                DeviceId = _creds.DeviceId;
            }

            // 2. open TCP connection
            try
            {
                var sslProtocol = KlassEndpoints.MqttUseTls ? MqttSslProtocols.TLSv1_2 : MqttSslProtocols.None;
                _client = new MqttClient(KlassEndpoints.MqttHost, KlassEndpoints.MqttPort, KlassEndpoints.MqttUseTls,
                    null, null, sslProtocol);
                SubscribeMqttPublish(_client);
                _client.ConnectionClosed += HandleConnectionClosed;

                // Last Will: broker auto-publishes when this client drops/keepalive-times-out
                var willTopic   = MqttTopics.Status(SchoolId, DeviceId);
                var willPayload = Encoding.UTF8.GetBytes("{\"online\":false,\"ts\":0}");
                byte code = _client.Connect(
                    _creds.ClientId,
                    _creds.Username,
                    _creds.Password,
                    /*willRetain*/ true,
                    /*willQos*/ (byte)1,
                    /*willFlag*/ true,
                    willTopic,
                    Encoding.UTF8.GetString(willPayload),
                    /*cleanSession*/ false,
                    /*keepAlivePeriod*/ (ushort)_creds.KeepaliveSeconds);

                if (code != 0)
                {
                    Debug.LogWarning($"[KlassMqtt] connect refused, code={code}");
                    SafeDisconnect();
                    yield break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] connect threw: {ex.GetType().Name}: {ex.Message} (host={KlassEndpoints.MqttHost}:{KlassEndpoints.MqttPort} tls={KlassEndpoints.MqttUseTls})");
                SafeDisconnect();
                yield break;
            }

            IsConnected = true;

            // 3. Subscribe to commands & session topics
            try
            {
                _client.Subscribe(
                    new[] {
                        MqttTopics.Cmd(SchoolId, DeviceId),
                        MqttTopics.SessionControl(SchoolId),
                        MqttTopics.Broadcast(SchoolId),
                    },
                    new[] { (byte)1, (byte)1, (byte)1 });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] subscribe failed: {ex.Message}");
            }

            learningOutbox = new LearningEventOutbox(SchoolId, DeviceId);
            nextOutboxSend = 0;
            // 4. Publish online presence (retained)
            try
            {
                var online = new JSONObject();
                online["online"] = true;
                online["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                _client.Publish(MqttTopics.Status(SchoolId, DeviceId),
                    Encoding.UTF8.GetBytes(online.ToString()),
                    (byte)1, /*retain*/ true);
            }
            catch (Exception ex) { Debug.LogWarning($"[KlassMqtt] presence publish failed: {ex.Message}"); }

            EnqueueMain(() => OnConnected?.Invoke());

            // 5. Schedule credential refresh
            if (_refreshLoop != null) StopCoroutine(_refreshLoop);
            _refreshLoop = StartCoroutine(CredentialRefreshLoop());
        }

        private IEnumerator CredentialRefreshLoop()
        {
            while (_wantConnected && IsConnected)
            {
                var ttl = _creds.ExpiresAt - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var sleep = Mathf.Max(60f, (float)(ttl - 60));
                yield return new WaitForSecondsRealtime(sleep);
                // Force reconnect with fresh creds (keeps things simple & robust)
                Debug.Log("[KlassMqtt] credential refresh triggered");
                _creds = null;
                SafeDisconnect();
                IsConnected = false;
                EnqueueMain(() => OnDisconnected?.Invoke());
                yield break;
            }
        }

        private void SubscribeMqttPublish(MqttClient client)
        {
            try
            {
                var ev = client.GetType().GetEvent("MqttMsgPublishReceived");
                if (ev == null) { Debug.LogError("[KlassMqtt] MqttMsgPublishReceived event not found on MqttClient"); return; }
                var mi = GetType().GetMethod("HandleIncomingMessage", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mi == null) { Debug.LogError("[KlassMqtt] HandleIncomingMessage method not found"); return; }
                var del = Delegate.CreateDelegate(ev.EventHandlerType, this, mi);
                ev.AddEventHandler(client, del);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[KlassMqtt] SubscribeMqttPublish threw: {ex.Message}");
            }
        }

        private void UnsubscribeMqttEvents(MqttClient client)
        {
            if (client == null) return;
            try
            {
                client.ConnectionClosed -= HandleConnectionClosed;
                UnsubscribeMqttPublish(client);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] Failed to unsubscribe connection event: {ex.Message}");
            }
        }

        private void UnsubscribeMqttPublish(MqttClient client)
        {
            if (client == null) return;
            try
            {
                var ev = client.GetType().GetEvent("MqttMsgPublishReceived");
                if (ev == null) return;
                var mi = GetType().GetMethod("HandleIncomingMessage", BindingFlags.NonPublic | BindingFlags.Instance);
                if (mi == null) return;
                var del = Delegate.CreateDelegate(ev.EventHandlerType, this, mi);
                ev.RemoveEventHandler(client, del);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] Failed to unsubscribe publish event: {ex.Message}");
            }
        }

        private void HandleConnectionClosed(object sender, EventArgs e)
        {
            try
            {
                EnqueueMain(() => { IsConnected = false; OnDisconnected?.Invoke(); });
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[KlassMqtt] Error in ConnectionClosed handler: {ex.Message}");
            }
        }

        [UnityEngine.Scripting.Preserve]
        private void HandleIncomingMessage(object sender, EventArgs e)
        {
            // M2Mqtt fires this on a worker thread — wrap in try-catch to prevent application crash on background thread exception.
            try
            {
                if (e == null) return;
                var eType = e.GetType();
                var topicProp = eType.GetProperty("Topic");
                var msgProp = eType.GetProperty("Message");
                if (topicProp == null || msgProp == null) return;

                string topic = topicProp.GetValue(e) as string ?? "";
                byte[] msgBytes = msgProp.GetValue(e) as byte[];
                string payload = msgBytes != null ? Encoding.UTF8.GetString(msgBytes) : "";

                EnqueueMain(() =>
                {
                    JSONNode node = null;
                    try { node = JSONNode.Parse(payload); } catch { /* not JSON */ }

                    if (topic == MqttTopics.SessionControl(SchoolId))      OnSessionControl?.Invoke(node);
                    else if (topic == MqttTopics.Broadcast(SchoolId))      OnBroadcast?.Invoke(node);
                    else if (topic == MqttTopics.Cmd(SchoolId, DeviceId))
                    {
                        if (node != null && node["action"].Value == "telemetryAck") learningOutbox?.Acknowledge(node);
                        else if (node != null && node["action"].Value == "softwareUpdate")
                        {
                            var accepted = Klever.VR.Updates.VrUpdateClient.Instance != null &&
                                Klever.VR.Updates.VrUpdateClient.Instance.RequestFleetUpdate();
                            PublishCmdAck(node["msgId"].Value, accepted, accepted ? null : "Updates require an idle headset. End the prepared class first.");
                        }
                        else OnCommand?.Invoke(node);
                    }
                });
            }
            catch (Exception ex)
            {
                // Background thread exceptions MUST be caught to prevent crash!
                Debug.LogWarning($"[KlassMqtt] HandleIncomingMessage exception: {ex.Message}");
            }
        }

        private void CleanupClient(MqttClient client)
        {
            if (client == null) return;
            UnsubscribeMqttEvents(client);
            try
            {
                if (client.IsConnected)
                {
                    // best-effort offline status before clean disconnect
                    try
                    {
                        var off = new JSONObject();
                        off["online"] = false;
                        off["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString();
                        client.Publish(MqttTopics.Status(SchoolId, DeviceId),
                            Encoding.UTF8.GetBytes(off.ToString()),
                            (byte)1, true);
                    }
                    catch { /* swallow */ }
                    client.Disconnect();
                }
            }
            catch { /* swallow */ }
        }

        private void DisconnectInternal(string reason)
        {
            CleanupClient(_client);
            _client = null;
            IsConnected = false;
            Debug.Log($"[KlassMqtt] disconnected: {reason}");
        }

        private void SafeDisconnect()
        {
            CleanupClient(_client);
            _client = null;
            IsConnected = false;
        }

        private void EnqueueMain(Action a) => _mainThread.Enqueue(a);
    }
}
