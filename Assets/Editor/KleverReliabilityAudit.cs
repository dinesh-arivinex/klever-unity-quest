using System;
using System.IO;
using System.Reflection;
using Klass.Net;
using SimpleJSON;
using UnityEngine;

namespace Klever.VR.Editor
{
    public static class ReliabilityAudit
    {
        private static void Require(bool value, string message)
        { if (!value) throw new Exception(message); }
        private static JSONObject Event(int sequence)
        {
            var node = new JSONObject();
            node["experienceId"] = "editor-offline-fixture";
            node["clientSessionId"] = "fixture-session";
            node["sequence"] = sequence.ToString();
            return node;
        }
        private static string Folder(LearningEventOutbox box) =>
            (string)typeof(LearningEventOutbox).GetField("directory", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(box);
        public static void Run()
        {
            // Offline only: no MQTT, device registration or learner records.
            var fixture = "editor-reliability-" + Guid.NewGuid().ToString("N");
            var box = new LearningEventOutbox(fixture, "offline");
            var folder = Folder(box);
            box.Add(Event(1));
            var first = Directory.GetFiles(folder, "*.pending")[0];
            File.Move(first, first + ".writing");
            File.WriteAllText(Path.Combine(folder, "broken.pending"), "{\"incomplete\":");
            File.WriteAllText(Path.Combine(folder, "truncated.pending.writing"), "{\"experienceId\":\"editor-offline-fixture\"");
            box = new LearningEventOutbox(fixture, "offline");
            Require(File.Exists(first), "Interrupted complete write did not recover.");
            int published = 0;
            box.SendPending(node => { Require(node["sequence"].Value == "1", "Unexpected recovered identity."); published++; });
            Require(published == 1, "Corrupt event blocked a valid event.");
            Require(Directory.GetFiles(folder, "*.quarantined-*").Length == 2, "Corrupt bytes were not preserved.");
            box.SendPending(node => published++);
            Require(published == 1, "Retry throttle ignored.");
            box.Acknowledge(new JSONObject());
            Require(File.Exists(first), "Invalid ACK removed a pending event.");
            box.Acknowledge(Event(1));
            box.Acknowledge(Event(1));
            box.Add(Event(1));
            Require(File.Exists(Path.ChangeExtension(first, ".acked")) && !File.Exists(first), "ACK/deduplication failed.");
            box.Add(Event(2));
            try { box.SendPending(node => { throw new IOException("Offline fixture transport error"); }); }
            catch (IOException) { }
            published = 0;
            box.SendPending(node => published++);
            Require(published == 1, "Transport failure lost or throttled a valid record.");
            bool rejected = false;
            try { box.Add(new JSONObject()); } catch (ArgumentException) { rejected = true; }
            Require(rejected, "Identity-free event accepted.");
            var capped = new LearningEventOutbox(fixture + "-batch", "offline");
            for (int i = 1; i <= 15; i++) capped.Add(Event(i));
            int batch = 0;
            capped.SendPending(node => batch++);
            Require(batch == 12, "Publish batch exceeded its frame budget.");
            capped.SendPending(node => batch++);
            Require(batch == 15, "Later records starved behind throttled records.");
            var mismatched = new LearningEventOutbox(fixture + "-identity", "offline");
            mismatched.Add(Event(1));
            var wrongFile = Directory.GetFiles(Folder(mismatched), "*.pending")[0];
            File.WriteAllText(wrongFile, Event(2).ToString());
            mismatched.SendPending(node => { throw new Exception("Changed record identity was published."); });
            Require(Directory.GetFiles(Folder(mismatched), "*.quarantined-*").Length == 1, "Changed identity not preserved.");
            foreach (var url in new[] { "klever://webtest", "klever://webtest/", "klever://webtest?return=1" })
                Require(DeepLinkManager.IsSimulationReturnUrl(url), "Valid return rejected.");
            foreach (var url in new[] { "https://evil.test/klever://webtest", "klever://webtest.evil", "klever://evil@webtest", "klever://webtest:80", "klever://webtest/other", "klever://webtest#spoof", "invalid", "" })
                Require(!DeepLinkManager.IsSimulationReturnUrl(url), "Spoofed return accepted.");
            var receiver = new GameObject("Offline return-link fixture").AddComponent<DeepLinkManager>();
            var handle = typeof(DeepLinkManager).GetMethod("onDeepLinkActivated", BindingFlags.Instance | BindingFlags.NonPublic);
            int oldSimulation = PlayerPrefs.GetInt("InSimulation", 0);
            bool oldResuming = DeepLinkManager.isResumingFromDeepLink;
            try
            {
                PlayerPrefs.SetInt("InSimulation", 0);
                DeepLinkManager.isResumingFromDeepLink = false;
                handle.Invoke(receiver, new object[] { "klever://webtest" });
                Require(!DeepLinkManager.isResumingFromDeepLink, "Return accepted outside simulation.");
                PlayerPrefs.SetInt("InSimulation", 1);
                handle.Invoke(receiver, new object[] { "https://evil.test/klever://webtest" });
                Require(!DeepLinkManager.isResumingFromDeepLink, "Substring attack activated resume.");
                handle.Invoke(receiver, new object[] { "klever://webtest" });
                Require(DeepLinkManager.isResumingFromDeepLink, "Active simulation return ignored.");
            }
            finally
            {
                PlayerPrefs.SetInt("InSimulation", oldSimulation);
                DeepLinkManager.isResumingFromDeepLink = oldResuming;
                UnityEngine.Object.DestroyImmediate(receiver.gameObject);
            }
            Debug.Log("[RELIABILITY-AUDIT] PASS: interrupted writes, corruption isolation, ACK idempotency, retry, identity validation and return-link guards. Offline evidence retained: " + folder);
        }
    }
}
