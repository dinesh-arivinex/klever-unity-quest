using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SimpleJSON;
using UnityEngine;

namespace Klass.Net
{
    // Persist before sending. Only a server acknowledgement after database writes
    // archives a record; MQTT PUBACK alone cannot prove database persistence.
    public sealed class LearningEventOutbox
    {
        private readonly string directory;
        private readonly Dictionary<string, float> sent = new Dictionary<string, float>();
        public LearningEventOutbox(string school, string device)
        {
            directory = Path.Combine(Application.persistentDataPath, "learning-events", Hash(school + ":" + device));
            Directory.CreateDirectory(directory);
            RecoverInterruptedWrites();
        }
        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "");
        }
        private static string Key(JSONNode node) => Hash(node["experienceId"].Value + ":" + node["clientSessionId"].Value + ":" + node["sequence"].Value);
        private static bool HasIdentity(JSONNode node)
        {
            return node != null && node.IsObject &&
                !string.IsNullOrWhiteSpace(node["experienceId"].Value) &&
                !string.IsNullOrWhiteSpace(node["clientSessionId"].Value) &&
                long.TryParse(node["sequence"].Value, out var sequence) && sequence > 0;
        }
        private JSONObject ReadRecord(string file)
        {
            var json = File.ReadAllText(file);
            JSONNode node;
            try
            {
                // SimpleJSON accepts some truncated objects. Validate with the
                // project's existing strict parser before recovering a write.
                Newtonsoft.Json.Linq.JObject.Parse(json);
                node = JSONNode.Parse(json);
            }
            catch (Exception error) when (!(error is OutOfMemoryException))
            { throw new FormatException("Invalid learning-event JSON.", error); }
            // Also verify the filename: a damaged identity must never acknowledge
            // or publish a different event under the original event's key.
            if (!HasIdentity(node) || Path.GetFileName(file).Split('.')[0] != Key(node))
                throw new FormatException("Invalid learning-event identity.");
            return node.AsObject;
        }
        private void Quarantine(string file)
        {
            // Preserve evidence rather than discard learner data. A bad record
            // must not prevent unrelated valid events from reaching the server.
            File.Move(file, file + ".quarantined-" + Guid.NewGuid().ToString("N"));
            sent.Remove(file);
            Debug.LogWarning("[LearningEventOutbox] Preserved a record for manual recovery.");
        }
        private void RecoverInterruptedWrites()
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.pending.writing"))
            {
                try
                {
                    ReadRecord(file);
                    var pending = file.Substring(0, file.Length - ".writing".Length);
                    if (File.Exists(pending) || File.Exists(Path.ChangeExtension(pending, ".acked")))
                        Quarantine(file);
                    else File.Move(file, pending);
                }
                catch (FormatException) { Quarantine(file); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                { Debug.LogWarning("[LearningEventOutbox] Interrupted record recovery will retry on next launch."); }
            }
        }
        public static JSONNode Normalize(JSONNode node)
        {
            if (ReferenceEquals(node, null) || node.IsNull) return new JSONNull();
            // This bundled JSONString constructor permits a null Value, while
            // its Escape() serializer dereferences Value.Length and throws.
            if (node.IsString) return new JSONString(node.Value ?? string.Empty);
            if (node.IsArray)
            {
                var array = new JSONArray();
                foreach (var child in node.Children) array.Add(Normalize(child));
                return array;
            }
            if (node.IsObject)
            {
                var obj = new JSONObject();
                foreach (KeyValuePair<string, JSONNode> pair in node.AsObject) obj[pair.Key] = Normalize(pair.Value);
                return obj;
            }
            return node;
        }
        public void Add(JSONObject node)
        {
            node = Normalize(node).AsObject;
            if (!HasIdentity(node)) throw new ArgumentException("Learning events require experience, session and positive sequence identifiers.");
            var file = Path.Combine(directory, Key(node) + ".pending");
            if (File.Exists(file) || File.Exists(Path.ChangeExtension(file, ".acked"))) return;
            var temporary = file + ".writing";
            var bytes = Encoding.UTF8.GetBytes(node.ToString());
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            File.Move(temporary, file);
        }
        public void SendPending(Action<JSONObject> publish)
        {
            int count = 0;
            foreach (var file in Directory.EnumerateFiles(directory, "*.pending"))
            {
                if (sent.TryGetValue(file, out var at) && Time.realtimeSinceStartup - at < 10f) continue;
                JSONObject record;
                try { record = ReadRecord(file); }
                catch (FormatException) { Quarantine(file); continue; }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                { Debug.LogWarning("[LearningEventOutbox] An unreadable record remains queued for retry."); continue; }
                // Transport errors propagate to the connection's retry loop;
                // never quarantine a valid record because publishing failed.
                publish(record);
                sent[file] = Time.realtimeSinceStartup;
                if (++count == 12) break;
            }
        }
        public void Acknowledge(JSONNode acknowledgement)
        {
            if (!HasIdentity(acknowledgement)) return;
            var file = Path.Combine(directory, Key(acknowledgement) + ".pending");
            var archive = Path.ChangeExtension(file, ".acked");
            if (File.Exists(file) && !File.Exists(archive)) File.Move(file, archive);
            sent.Remove(file);
        }
    }
}
