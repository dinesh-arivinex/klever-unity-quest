using System.IO;
using UnityEditor.Android;

namespace Klever.VR.Editor
{
    // Java entry points are invoked by JNI name; keep them when a future private
    // release enables minification. Preserve every existing SDK rule.
    public sealed class UpdaterAndroidBuild : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1000;
        private const string Rule = "-keep class com.klever.updates.** { *; }";
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var file = Path.Combine(path, "proguard-unity.txt");
            var existing = File.Exists(file) ? File.ReadAllText(file) : string.Empty;
            if (!existing.Contains(Rule)) File.AppendAllText(file, "\n# Klever updater JNI and installer result receiver\n" + Rule + "\n");
        }
    }
}
