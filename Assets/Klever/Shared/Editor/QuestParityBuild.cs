using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Klever.VR.Editor
{
    public static class QuestParityBuild
    {
        public static void BuildTestAndroid()
        {
            UpdateAudit.Run();
            ReliabilityAudit.Run();
            QuestParityAudit.Run();
            var sdk = Environment.GetEnvironmentVariable("KLEVER_ANDROID_SDK_ROOT");
            if (!string.IsNullOrEmpty(sdk)) UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath = sdk;
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
                throw new BuildFailedException("Select Android before building Quest.");
            var output = Environment.GetEnvironmentVariable("KLEVER_QUEST_BUILD_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = "Builds/Quest/Klever-" + PlayerSettings.bundleVersion + "-quest3-build" + Environment.GetEnvironmentVariable("KLEVER_ANDROID_VERSION_CODE") + ".apk";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var releaseIdentifier = PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.Android);
            var releaseName = PlayerSettings.productName;
            var releaseSigning = PlayerSettings.Android.useCustomKeystore;
            // UAT builds coexist with the release and never require its private key.
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.klever.vr.quest.paritytest");
            PlayerSettings.productName = "Klever";
            PlayerSettings.Android.useCustomKeystore = false;
            BuildReport report;
            try { using (var version = new AndroidBuildVersion()) report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                target = BuildTarget.Android,
                locationPathName = output,
                options = BuildOptions.Development | BuildOptions.CompressWithLz4HC
            }); }
            finally
            {
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, releaseIdentifier);
                PlayerSettings.productName = releaseName;
                PlayerSettings.Android.useCustomKeystore = releaseSigning;
                AssetDatabase.SaveAssets();
            }
            UnityEngine.Debug.Log("[QUEST-PARITY-BUILD] Build=" + report.summary.result + " errors=" + report.summary.totalErrors + " output=" + Path.GetFullPath(output));
            if (report.summary.result != BuildResult.Succeeded || report.summary.totalErrors != 0)
                throw new BuildFailedException("Quest parity build failed. Inspect the log.");
        }
    }
}
