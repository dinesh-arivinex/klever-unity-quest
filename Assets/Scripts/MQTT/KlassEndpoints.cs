using UnityEngine;

namespace Klass.Net
{
    /// <summary>
    /// Centralised endpoint configuration for the headset.
    ///
    /// Reads from PlayerPrefs first (key: "klass.backend_base_url" / "klass.mqtt_broker_host" /
    /// "klass.mqtt_broker_port"), then from compile-time defaults.
    /// This removes the hard-coded IPs that were previously scattered across scripts
    /// (DeviceSynHandler / API / GamificationManager / DeviceIDManager).
    /// </summary>
    public static class KlassEndpoints
    {
        // ---- Compile-time defaults (override per build via PlayerPrefs) ----
        private const string DefaultBackendBase = "https://api.klever-edu.com/";
        private const string DefaultMqttHost = "api.klever-edu.com";
        private const int DefaultMqttPort = 1883;
        private const bool DefaultMqttUseTls = false;

        public static string BackendBase
        {
            get
            {
                var v = PlayerPrefs.GetString("klass.backend_base_url", DefaultBackendBase);
                return string.IsNullOrEmpty(v) ? DefaultBackendBase : v.TrimEnd('/');
            }
        }

        public static string MqttHost =>
            PlayerPrefs.GetString("klass.mqtt_broker_host", DefaultMqttHost);

        public static int MqttPort =>
            PlayerPrefs.GetInt("klass.mqtt_broker_port", DefaultMqttPort);

        public static bool MqttUseTls =>
            PlayerPrefs.GetInt("klass.mqtt_use_tls", DefaultMqttUseTls ? 1 : 0) == 1;

        public static string V1(string path)
        {
            if (string.IsNullOrEmpty(path)) return BackendBase + "/v1/";
            return BackendBase + "/v1/" + path.TrimStart('/');
        }
    }
}
