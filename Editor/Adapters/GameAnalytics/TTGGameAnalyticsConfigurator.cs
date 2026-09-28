using System;
using TripleTapGames.Foundation.Adapters.GameAnalytics;
using UnityEditor;
using UnityEngine;

namespace TripleTapGames.Foundation.Editor
{
    [InitializeOnLoad]
    public static class TTGGameAnalyticsConfigurator
    {
        static TTGGameAnalyticsConfigurator() => TTGConfigurationHooks.Applying += Apply;

        public static void Apply(TTGProjectConfig config)
        {
            if (config == null || !config.GameAnalytics.Enabled) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Stop Play mode before applying GameAnalytics configuration.");
            var settings = GameAnalyticsSDK.GameAnalytics.SettingsGA;
            if (TTGGameAnalyticsConfiguration.Apply(config.GameAnalytics, settings))
                EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[TTG:Setup] Android and iOS GameAnalytics settings applied. Credential values are not logged.");
        }
    }
}
