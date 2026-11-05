using System;
using System.Globalization;

namespace TripleTapGames.Foundation
{
    /// <summary>Optional remote rule source. Local assets are never mutated.</summary>
    public static class TTGAdRemoteConfig
    {
        private static Func<string, string> reader;
        public static void SetReader(Func<string, string> read) => reader = read;
        public static void ClearReader() => reader = null;

        public static TTGInterstitialRules Resolve(TTGInterstitialRules defaults)
        {
            if (defaults == null) return null;
            return Resolve(defaults, reader);
        }

        internal static TTGInterstitialRules Resolve(TTGInterstitialRules defaults, Func<string, string> read)
        {
            if (read == null) return defaults;
            try
            {
                return new TTGInterstitialRules
                {
                    Enabled = bool.TryParse(read("interstitial_enabled"), out var enabled) ? enabled : defaults.Enabled,
                    MinimumSessionTime = Seconds(read("interstitial_min_session_seconds"), defaults.MinimumSessionTime),
                    LevelInterval = int.TryParse(read("AdLevelStart"), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out var level) && level >= 1 ? level : defaults.LevelInterval,
                    CooldownSeconds = Seconds(read("AdCoolDown"), defaults.CooldownSeconds)
                };
            }
            catch (Exception) { return defaults; } // Optional remote data must not break an ad request.
        }

        private static float Seconds(string value, float fallback) =>
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                && !float.IsNaN(seconds) && !float.IsInfinity(seconds) && seconds >= 0f ? seconds : fallback;
    }
}
