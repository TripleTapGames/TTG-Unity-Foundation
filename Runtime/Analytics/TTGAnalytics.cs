using System;
using System.Collections.Generic;

namespace TripleTapGames.Foundation
{
    public static class TTGEventNames
    {
        public const string LevelStart = "level_start";
        public const string LevelComplete = "level_complete";
        public const string LevelFail = "level_fail";
        public const string AdImpression = "ad_impression";
        public const string Purchase = "purchase";
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
        private static readonly List<ITTGAnalyticsProvider> Providers = new List<ITTGAnalyticsProvider>();

        public static void RegisterProvider(ITTGAnalyticsProvider provider)
        {
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            Providers.RemoveAll(item => item.ProviderName == provider.ProviderName);
            Providers.Add(provider);
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
                try
                {
                    provider.LogEvent(eventName, parameters);
                }
                catch (Exception exception)
                {
                    TTGLogger.Error(TTGLogCategory.Analytics, provider.ProviderName + " rejected an event.", exception);
                }
            }
        }

        public static void LevelStarted(object levelId) => LogLevel(TTGEventNames.LevelStart, levelId);
        public static void LevelCompleted(object levelId) => LogLevel(TTGEventNames.LevelComplete, levelId);
        public static void LevelFailed(object levelId) => LogLevel(TTGEventNames.LevelFail, levelId);

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

        internal static void ClearProviders() => Providers.Clear();
    }
}
