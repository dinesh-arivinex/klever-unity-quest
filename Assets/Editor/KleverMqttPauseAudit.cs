using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
namespace Klever.VR.Editor
{
    public static class MqttPauseAudit
    {
        public static void Run()
        {
            var go = new GameObject("Paused MQTT reconnect audit");
            var client = go.AddComponent<Klass.Net.KlassMqttClient>();
            var type = client.GetType();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("_wantConnected", flags).SetValue(client, true);
            type.GetProperty("IsConnected").SetValue(client, true);
            var loop = (IEnumerator)type.GetMethod("ReconnectLoop", flags).Invoke(client, null);
            float originalScale = Time.timeScale;
            try
            {
                Time.timeScale = 0;
                if (!loop.MoveNext() || !(loop.Current is WaitForSecondsRealtime))
                    throw new Exception("MQTT reconnect polling freezes with the paused lesson clock.");
                var wait = (WaitForSecondsRealtime)loop.Current;
                wait.waitTime = 0.01f;
                if (!wait.keepWaiting) throw new Exception("Realtime wait did not start.");
                System.Threading.Thread.Sleep(25);
                if (wait.keepWaiting) throw new Exception("MQTT polling cannot continue at timeScale zero.");
            }
            finally
            {
                Time.timeScale = originalScale;
                type.GetField("_wantConnected", flags).SetValue(client, false);
                type.GetProperty("IsConnected").SetValue(client, false);
                UnityEngine.Object.DestroyImmediate(go);
            }
            Debug.Log("[MQTT-PAUSE-AUDIT] PASS: reconnect polling expires at timeScale zero without a network connection.");
        }
    }
}