using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Klever.Branding.Editor
{
    public sealed class KleverAppIcons : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;
        public void OnPreprocessBuild(BuildReport report) { Apply(); }

        [MenuItem("Klever/Apply Flutter App Icons")]
        public static void Apply()
        {
            const string root = "Assets/Klever/Branding/";
            AssetDatabase.ImportAsset(root + "launcher_icon.png", ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(root + "launcher_foreground.png", ImportAssetOptions.ForceSynchronousImport);
            var legacy = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "launcher_icon.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(root + "launcher_foreground.png");
            if (legacy == null || foreground == null) throw new BuildFailedException("Missing Klever launcher artwork.");
            var backgroundPath = root + "launcher_background.png";
            if (!File.Exists(backgroundPath))
            {
                var backgroundImage = new Texture2D(64, 64, TextureFormat.RGB24, false);
                backgroundImage.SetPixels(Enumerable.Repeat(new Color32(9, 9, 9, 255), 64 * 64).Select(c => (Color)c).ToArray());
                backgroundImage.Apply();
                File.WriteAllBytes(backgroundPath, backgroundImage.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(backgroundImage);
            }
            AssetDatabase.ImportAsset(backgroundPath, ImportAssetOptions.ForceSynchronousImport);
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(backgroundPath);
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown,
                Enumerable.Repeat(legacy, PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Unknown).Length).ToArray());
            foreach (var kind in new[] { AndroidPlatformIconKind.Legacy, AndroidPlatformIconKind.Round, AndroidPlatformIconKind.Adaptive })
            {
                var icons = PlayerSettings.GetPlatformIcons(BuildTargetGroup.Android, kind);
                if (icons.Length == 0) throw new BuildFailedException("No Android icon slots: " + kind);
                foreach (var icon in icons)
                {
                    if (kind == AndroidPlatformIconKind.Adaptive)
                        icon.SetTextures(new[] { background, foreground });
                    else icon.SetTexture(legacy);
                }
                PlayerSettings.SetPlatformIcons(BuildTargetGroup.Android, kind, icons);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[KLEVER-ICONS] Default, Android legacy, round and adaptive slots assigned from Flutter artwork.");
        }
    }
}
