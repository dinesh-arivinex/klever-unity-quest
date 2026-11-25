using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Klever.VR.Editor
{
    public static class BuildSizeAudit
    {
        [Serializable] private class Entry { public string path; public long bytes; }
        [Serializable] private class Audit { public string output; public long totalBytes; public Entry[] largestPackedAssets; public string[] sceneDependencies; }
        public static void Write()
        {
            const string reportFile = "Library/LastBuild.buildreport";
            var report = File.Exists(reportFile)
                ? UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(reportFile).OfType<BuildReport>().FirstOrDefault()
                : null;
            if (report == null) throw new InvalidOperationException("Build once before auditing package size.");
            var file = Environment.GetEnvironmentVariable("KLEVER_BUILD_SIZE_AUDIT_OUTPUT");
            if (string.IsNullOrEmpty(file)) throw new InvalidOperationException("Set KLEVER_BUILD_SIZE_AUDIT_OUTPUT.");
            WriteReport(report, file);
        }
        public static void WriteReport(BuildReport report, string file)
        {
            var dependencies = AssetDatabase.GetDependencies(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(), true);
            var rows = report.packedAssets.SelectMany(p => p.contents).GroupBy(p => p.sourceAssetPath)
                .Select(g => new Entry { path = g.Key, bytes = g.Sum(p => (long)p.packedSize) }).OrderByDescending(p => p.bytes).Take(40).ToArray();
            var audit = new Audit { output = report.summary.outputPath, totalBytes = (long)report.summary.totalSize, largestPackedAssets = rows, sceneDependencies = dependencies };
            File.WriteAllText(file, JsonUtility.ToJson(audit, true));
            if (report.summary.totalSize > 500UL * 1024 * 1024)
                Debug.LogWarning("[KLEVER-SIZE-AUDIT] Installer exceeds the project's 500 MiB review threshold. Review the size report before publishing.");
            Debug.Log("[KLEVER-SIZE-AUDIT] Report written; no runtime assets changed.");
        }
    }
    public sealed class BuildSizeAuditHook : IPostprocessBuildWithReport
    {
        public int callbackOrder => 10000;
        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android && !string.IsNullOrEmpty(report.summary.outputPath))
                BuildSizeAudit.WriteReport(report, report.summary.outputPath + ".size-audit.json");
        }
    }
}
