using System;
using GameAnalyticsSDK.Setup;
using UnityEngine;

namespace TripleTapGames.Foundation.Adapters.GameAnalytics
{
    internal static class TTGGameAnalyticsConfiguration
    {
        internal static bool Apply(TTGGameAnalyticsConfig config, Settings settings)
        {
            if (config == null || settings == null)
                throw new InvalidOperationException("GameAnalytics configuration or settings asset is missing. Apply Configuration before building.");
            var platforms = new[] { RuntimePlatform.Android, RuntimePlatform.IPhonePlayer };
            var keys = new[] { Clean(config.AndroidGameKey), Clean(config.IosGameKey) };
            var secrets = new[] { Clean(config.AndroidSecretKey), Clean(config.IosSecretKey) };
            if (SameNonEmpty(keys[0], keys[1]) || SameNonEmpty(secrets[0], secrets[1]))
                throw new InvalidOperationException("GameAnalytics requires distinct Android and iOS credentials. Check both platform entries.");

            // Check all conflicts before modifying the vendor asset; never log values.
            for (var p = 0; p < platforms.Length; p++)
            {
                var count = 0;
                for (var i = 0; i < settings.Platforms.Count; i++)
                {
                    if (settings.Platforms[i] == platforms[p]) count++;
                    if (settings.Platforms[i] == platforms[0] || settings.Platforms[i] == platforms[1]) continue;
                    if (SameNonEmpty(keys[p], settings.GetGameKey(i)) || SameNonEmpty(secrets[p], settings.GetSecretKey(i)))
                        throw new InvalidOperationException("GameAnalytics credentials conflict with another configured platform. Review the vendor settings.");
                }
                if (count > 1) throw new InvalidOperationException("GameAnalytics settings contain duplicate mobile platform entries. Remove duplicates before applying.");
            }

            var changed = false;
            var indexes = new int[2];
            for (var p = 0; p < platforms.Length; p++)
            {
                indexes[p] = settings.Platforms.IndexOf(platforms[p]);
                if (indexes[p] < 0)
                {
                    settings.AddPlatform(platforms[p]);
                    indexes[p] = settings.Platforms.Count - 1;
                    changed = true;
                }
                changed |= settings.GetGameKey(indexes[p]) != keys[p] || settings.GetSecretKey(indexes[p]) != secrets[p];
            }
            if (!changed) return false;
            // Clear both mobile entries first so a legitimate swap of keys is supported.
            foreach (var index in indexes)
            {
                settings.UpdateGameKey(index, "");
                settings.UpdateSecretKey(index, "");
            }
            for (var p = 0; p < platforms.Length; p++)
            {
                settings.UpdateGameKey(indexes[p], keys[p]);
                settings.UpdateSecretKey(indexes[p], secrets[p]);
                if (settings.GetGameKey(indexes[p]) != keys[p] || settings.GetSecretKey(indexes[p]) != secrets[p])
                    throw new InvalidOperationException("GameAnalytics rejected the platform settings. Review the configuration.");
            }
            return true;
        }

        private static string Clean(string value) => (value ?? "").Trim();
        private static bool SameNonEmpty(string a, string b) => !string.IsNullOrEmpty(a) && a == b;
    }
}
