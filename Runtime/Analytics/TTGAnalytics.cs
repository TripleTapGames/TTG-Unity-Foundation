using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace TripleTapGames.Foundation
{
    public static class TTGEventNames
    {
        public const string LevelStart = "level_start";
        public const string LevelComplete = "level_complete";
        public const string LevelFail = "level_fail";
        public const string AdImpression = "ad_impression";
        public const string Purchase = "purchase";

        public static string Retention(int day) => "day_" + Math.Max(0, day) + "_retention";
        public static string MilestoneLevelCompleted(int levelNumber) => "level_" + Math.Max(0, levelNumber) + "_completed";
    }

    public sealed class TTGAdImpression
    {
        public string AdSource;
        public string NetworkName;
        public string AdFormat;
        public string Placement;
        public string AdUnitId;
        public string Currency = "USD";
        public double Revenue;
    }

    public interface ITTGAnalyticsProvider
    {
        string ProviderName { get; }
        bool IsInitialized { get; }
        void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null);
    }

    public static class TTGAnalytics
    {
        internal const string FirstLoginDateKey = "TTG.Foundation.Analytics.FirstLoginDateUtc.v1";
        internal const string LastRetentionDayKey = "TTG.Foundation.Analytics.LastRetentionDay.v1";
        private const string MilestoneKeyPrefix = "TTG.Foundation.Analytics.MilestoneLevel.";
        private static readonly HashSet<int> MilestoneLevels = new HashSet<int> { 5, 10, 15, 20 };
        private static readonly List<ITTGAnalyticsProvider> Providers = new List<ITTGAnalyticsProvider>();
        private static readonly HashSet<string> RetentionProviders = new HashSet<string>();
        private static string pendingRetentionEvent;
        private static int pendingRetentionDay = -1;

        public static void RegisterProvider(ITTGAnalyticsProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            Providers.RemoveAll(item => item.ProviderName == provider.ProviderName);
            Providers.Add(provider);
            SendPendingRetention(provider);
        }

        public static void UnregisterProvider(ITTGAnalyticsProvider provider)
        {
            if (provider != null) Providers.Remove(provider);
        }

        public static void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (string.IsNullOrWhiteSpace(eventName)) throw new ArgumentException("An event name is required.", nameof(eventName));
            for (var i = 0; i < Providers.Count; i++)
            {
                var provider = Providers[i];
                if (!provider.IsInitialized) continue;
                SendToProvider(provider, eventName, parameters);
            }
        }

        public static void LevelStarted(object levelId) => LogLevel(TTGEventNames.LevelStart, levelId);
        public static void LevelCompleted(object levelId) => LogLevel(TTGEventNames.LevelComplete, levelId);
        public static void LevelCompleted(object levelId, int levelNumber)
        {
            LevelCompleted(levelId);
            MilestoneCompleted(levelNumber);
        }
        public static void LevelFailed(object levelId) => LogLevel(TTGEventNames.LevelFail, levelId);

        public static void MilestoneCompleted(int levelNumber)
        {
            if (!MilestoneLevels.Contains(levelNumber)) return;
            var key = MilestoneKeyPrefix + levelNumber;
            if (PlayerPrefs.GetInt(key, 0) == 1) return;
            LogEvent(TTGEventNames.MilestoneLevelCompleted(levelNumber));
            PlayerPrefs.SetInt(key, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Sends day_N_retention once per UTC day. TTGInitializer invokes this after
        /// analytics providers initialize; custom bootstraps may safely call it again.
        /// </summary>
        public static void RecordSessionRetention() => RecordSessionRetention(DateTime.UtcNow);

        public static void Purchase(string productId, decimal localizedPrice, string currency, string transactionId)
        {
            LogEvent(TTGEventNames.Purchase, new Dictionary<string, object>
            {
                { "product_id", productId },
                { "price", localizedPrice },
                { "currency", currency },
                { "transaction_id", transactionId }
            });
        }

        public static void AdImpression(TTGAdImpression impression)
        {
            if (impression == null) return;
            LogEvent(TTGEventNames.AdImpression, new Dictionary<string, object>
            {
                { "ad_source", impression.AdSource },
                { "network", impression.NetworkName },
                { "format", impression.AdFormat },
                { "placement", impression.Placement },
                { "ad_unit_id", impression.AdUnitId },
                { "currency", impression.Currency },
                { "revenue", impression.Revenue }
            });
        }

        private static void LogLevel(string eventName, object levelId)
        {
            LogEvent(eventName, new Dictionary<string, object> { { "level_id", levelId?.ToString() ?? string.Empty } });
        }

        internal static void RecordSessionRetention(DateTime utcNow)
        {
            if (pendingRetentionEvent == null)
            {
                var today = utcNow.ToUniversalTime().Date;
                DateTime firstLogin;
                if (!PlayerPrefs.HasKey(FirstLoginDateKey)
                    || !DateTime.TryParseExact(PlayerPrefs.GetString(FirstLoginDateKey), "yyyy-MM-dd",
                        CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out firstLogin))
                {
                    firstLogin = today;
                    PlayerPrefs.SetString(FirstLoginDateKey, today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                    PlayerPrefs.Save();
                }

                pendingRetentionDay = Math.Max(0, (today - firstLogin.Date).Days);
                if (PlayerPrefs.GetInt(LastRetentionDayKey, -1) == pendingRetentionDay) return;
                pendingRetentionEvent = TTGEventNames.Retention(pendingRetentionDay);
            }

            for (var i = 0; i < Providers.Count; i++) SendPendingRetention(Providers[i]);
        }

        private static void SendPendingRetention(ITTGAnalyticsProvider provider)
        {
            if (string.IsNullOrEmpty(pendingRetentionEvent) || provider == null || !provider.IsInitialized
                || RetentionProviders.Contains(provider.ProviderName)) return;
            SendToProvider(provider, pendingRetentionEvent, null);
            RetentionProviders.Add(provider.ProviderName);
            PlayerPrefs.SetInt(LastRetentionDayKey, pendingRetentionDay);
            PlayerPrefs.Save();
        }

        private static void SendToProvider(ITTGAnalyticsProvider provider, string eventName,
            IReadOnlyDictionary<string, object> parameters)
        {
            try
            {
                provider.LogEvent(eventName, parameters);
            }
            catch (Exception exception)
            {
                TTGLogger.Error(TTGLogCategory.Analytics, provider.ProviderName + " rejected an event.", exception);
            }
        }

        internal static void ClearProviders()
        {
            Providers.Clear();
            RetentionProviders.Clear();
            pendingRetentionEvent = null;
            pendingRetentionDay = -1;
        }
    }
}
