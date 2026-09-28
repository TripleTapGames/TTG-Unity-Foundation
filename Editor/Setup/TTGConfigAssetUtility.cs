using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace TripleTapGames.Foundation.Editor
{
    internal static class TTGConfigAssetUtility
    {
        internal const string GeneratedRoot = "Assets/TTGGenerated";
        internal const string ResourceRoot = GeneratedRoot + "/Resources/TTG";
        internal const string ProjectConfigPath = ResourceRoot + "/TTGProjectConfig.asset";
        internal const string AdsConfigPath = ResourceRoot + "/TTAdsConfig.asset";

        internal static TTGProjectConfig GetOrCreateProjectConfig()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TTGProjectConfig>(ProjectConfigPath);
            if (asset != null) return asset;
            EnsureFolders();
            asset = ScriptableObject.CreateInstance<TTGProjectConfig>();
            AssetDatabase.CreateAsset(asset, ProjectConfigPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        internal static TTAdsConfig GetOrCreateAdsConfig()
        {
            var asset = AssetDatabase.LoadAssetAtPath<TTAdsConfig>(AdsConfigPath);
            if (asset != null) return asset;
            EnsureFolders();
            asset = ScriptableObject.CreateInstance<TTAdsConfig>();
            AssetDatabase.CreateAsset(asset, AdsConfigPath);
            AssetDatabase.SaveAssets();
            return asset;
        }

        internal static void Select(UnityEngine.Object asset)
        {
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);
        }

        internal static void ImportEnvironment(TTGProjectConfig config)
        {
            if (config == null) return;
            config.Facebook.AppId = Read("TTG_FACEBOOK_APP_ID", config.Facebook.AppId);
            config.Facebook.ClientToken = Read("TTG_FACEBOOK_CLIENT_TOKEN", config.Facebook.ClientToken);
            config.AppLovin.SdkKey = Read("TTG_APPLOVIN_SDK_KEY", config.AppLovin.SdkKey);
            config.Singular.ApiKey = Read("TTG_SINGULAR_API_KEY", config.Singular.ApiKey);
            config.Singular.ApiSecret = Read("TTG_SINGULAR_API_SECRET", config.Singular.ApiSecret);
            config.GameAnalytics.AndroidGameKey = Read("TTG_GA_ANDROID_GAME_KEY", config.GameAnalytics.AndroidGameKey);
            config.GameAnalytics.AndroidSecretKey = Read("TTG_GA_ANDROID_SECRET", config.GameAnalytics.AndroidSecretKey);
            config.GameAnalytics.IosGameKey = Read("TTG_GA_IOS_GAME_KEY", config.GameAnalytics.IosGameKey);
            config.GameAnalytics.IosSecretKey = Read("TTG_GA_IOS_SECRET", config.GameAnalytics.IosSecretKey);
            CopyFirebaseFile("TTG_FIREBASE_ANDROID_CONFIG_PATH", "Assets/google-services.json");
            CopyFirebaseFile("TTG_FIREBASE_IOS_CONFIG_PATH", "Assets/GoogleService-Info.plist");
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            Debug.Log("[TTG:Setup] Environment values imported. Credential values were not logged.");
        }

        private static string Read(string name, string current)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(value) ? current : value;
        }

        private static void CopyFirebaseFile(string variable, string destination)
        {
            var source = Environment.GetEnvironmentVariable(variable);
            if (string.IsNullOrEmpty(source) || !File.Exists(source)) return;
            File.Copy(source, destination, true);
            AssetDatabase.ImportAsset(destination);
        }

        private static void EnsureFolders()
        {
            Directory.CreateDirectory(ResourceRoot);
            var ignorePath = Path.Combine(GeneratedRoot, ".gitignore");
            if (!File.Exists(ignorePath)) File.WriteAllText(ignorePath, "*\n!.gitignore\n");
            AssetDatabase.Refresh();
        }
    }
}
