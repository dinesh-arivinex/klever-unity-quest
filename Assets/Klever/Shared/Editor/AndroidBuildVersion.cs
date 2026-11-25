using System;
using System.Globalization;
using UnityEditor;
using UnityEditor.Build;

namespace Klever.VR.Editor
{
    // The publisher enforces continuity with the actual registry. This scope
    // additionally prevents accidentally producing another version-code-1 APK.
    public sealed class AndroidBuildVersion : IDisposable
    {
        private readonly int previous;
        public static int Validate(string requested, int baseline)
        {
            if (!int.TryParse(requested, NumberStyles.None, CultureInfo.InvariantCulture, out var code) ||
                code <= baseline || code > 2100000000)
                throw new BuildFailedException("Set KLEVER_ANDROID_VERSION_CODE to an integer above the project baseline and the last published version (maximum 2100000000).");
            return code;
        }
        public AndroidBuildVersion()
        {
            previous = PlayerSettings.Android.bundleVersionCode;
            PlayerSettings.Android.bundleVersionCode = Validate(Environment.GetEnvironmentVariable("KLEVER_ANDROID_VERSION_CODE"), previous);
        }
        public void Dispose() { PlayerSettings.Android.bundleVersionCode = previous; AssetDatabase.SaveAssets(); }
    }
}
