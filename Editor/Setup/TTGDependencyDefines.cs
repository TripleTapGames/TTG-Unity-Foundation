using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;

namespace TripleTapGames.Foundation.Editor
{
    internal static class TTGDependencyDefines
    {
        private static readonly string[] Managed =
        {
            "TTG_FIREBASE", "TTG_FACEBOOK", "TTG_GAMEANALYTICS", "TTG_SINGULAR", "TTG_IAP"
        };

        internal static void ApplyForActiveTarget()
        {
            var group = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            var symbols = new HashSet<string>(PlayerSettings.GetScriptingDefineSymbolsForGroup(group)
                .Split(';').Where(item => !string.IsNullOrWhiteSpace(item)));
            foreach (var managed in Managed) symbols.Remove(managed);

            var packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Select(item => item.name).ToHashSet();
            // Firebase compiles only through the ignored local adapter, never a saved PlayerSettings define.
            if (File.Exists("Assets/FacebookSDK/Plugins/Facebook.Unity.dll")) symbols.Add("TTG_FACEBOOK");
            if (packages.Contains("com.gameanalytics.sdk") && !Directory.Exists("Assets/GameAnalytics")) symbols.Add("TTG_GAMEANALYTICS");
            if (packages.Contains("singular-unity-package")) symbols.Add("TTG_SINGULAR");
            if (packages.Contains("com.unity.purchasing")) symbols.Add("TTG_IAP");

            PlayerSettings.SetScriptingDefineSymbolsForGroup(group, string.Join(";", symbols.OrderBy(item => item)));
        }
    }
}
