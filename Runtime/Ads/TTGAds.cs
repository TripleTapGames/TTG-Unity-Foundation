using System;
using UnityEngine;

namespace TripleTapGames.Foundation
{
    public enum TTGAdPlacement
    {
        LevelComplete,
        LevelFailed,
        RestartLevel,
        ExtraCoins,
        MainMenu,
        PauseMenu,
        Shop,
        Custom
    }

    public enum TTGAdResultStatus
    {
        Completed,
        Rewarded,
        Closed,
        NotReady,
        Disabled,
        BlockedByRules,
        AlreadyShowing,
        Failed
    }

    public sealed class TTGAdResult
    {
        public TTGAdResultStatus Status { get; }
        public string Message { get; }
        public bool RewardEarned => Status == TTGAdResultStatus.Rewarded;

        public TTGAdResult(TTGAdResultStatus status, string message = null)
        {
            Status = status;
            Message = message ?? string.Empty;
        }
    }

    public interface ITTGAdsProvider
    {
        bool IsInterstitialReady { get; }
        bool IsRewardedReady { get; }
        void ShowInterstitial(string adUnitId, TTGAdPlacement placement, Action<TTGAdResult> completed);
        void ShowRewarded(string adUnitId, TTGAdPlacement placement, Action rewarded, Action<TTGAdResult> closed);
        void ShowBanner(string adUnitId, TTGBannerPosition position);
        void HideBanner(string adUnitId);
        void ShowMrec(string adUnitId);
        void HideMrec(string adUnitId);
    }

    public static class TTGAds
    {
        private static ITTGAdsProvider provider;
        private static TTAdsConfig config;
        private static bool interstitialShowing;
        private static bool rewardedShowing;
        private static int completedLevels;
        private static double sessionStartedAt;
        private static double lastInterstitialAt = double.NegativeInfinity;
        internal static Func<double> Clock = () => Time.realtimeSinceStartupAsDouble;

        public static bool IsInterstitialReady => provider != null && provider.IsInterstitialReady && IsInterstitialAllowed();
        public static bool IsRewardedReady => provider != null && provider.IsRewardedReady && config != null && config.AdsEnabled && config.RewardedEnabled;

        internal static void Configure(ITTGAdsProvider adsProvider, TTAdsConfig adsConfig)
        {
            provider = adsProvider;
            config = adsConfig;
            sessionStartedAt = Clock();
            completedLevels = 0;
            interstitialShowing = false;
            rewardedShowing = false;
        }

        public static void NotifyLevelCompleted(int levelNumber = -1)
        {
            completedLevels = levelNumber > 0 ? levelNumber : completedLevels + 1;
        }

        public static void ShowInterstitial(TTGAdPlacement placement, Action<TTGAdResult> completed = null)
        {
            if (interstitialShowing) { completed?.Invoke(new TTGAdResult(TTGAdResultStatus.AlreadyShowing)); return; }
            if (!IsInterstitialAllowed()) { completed?.Invoke(new TTGAdResult(TTGAdResultStatus.BlockedByRules)); return; }
            if (provider == null || !provider.IsInterstitialReady) { completed?.Invoke(new TTGAdResult(TTGAdResultStatus.NotReady)); return; }

            interstitialShowing = true;
            var once = new Once<TTGAdResult>(result =>
            {
                interstitialShowing = false;
                lastInterstitialAt = Clock();
                completed?.Invoke(result);
            });
            provider.ShowInterstitial(config.GetCurrentPlatformUnits().InterstitialId, placement, once.Invoke);
        }

        public static void ShowRewarded(TTGAdPlacement placement, Action onRewarded, Action<TTGAdResult> onClosed = null)
        {
            if (rewardedShowing) { onClosed?.Invoke(new TTGAdResult(TTGAdResultStatus.AlreadyShowing)); return; }
            if (!IsRewardedReady) { onClosed?.Invoke(new TTGAdResult(TTGAdResultStatus.NotReady)); return; }

            rewardedShowing = true;
            var rewardOnce = new Once(onRewarded);
            var closeOnce = new Once<TTGAdResult>(result =>
            {
                rewardedShowing = false;
                onClosed?.Invoke(result);
            });
            provider.ShowRewarded(config.GetCurrentPlatformUnits().RewardedId, placement, rewardOnce.Invoke, closeOnce.Invoke);
        }

        public static void ShowBanner(TTGBannerPosition? position = null)
        {
            if (provider == null || config == null || !config.AdsEnabled || !config.BannerEnabled) return;
            provider.ShowBanner(config.GetCurrentPlatformUnits().BannerId, position ?? config.DefaultBannerPosition);
        }

        public static void HideBanner()
        {
            if (provider != null && config != null) provider.HideBanner(config.GetCurrentPlatformUnits().BannerId);
        }

        public static void ShowMrec()
        {
            if (provider == null || config == null || !config.AdsEnabled || !config.MrecEnabled) return;
            provider.ShowMrec(config.GetCurrentPlatformUnits().MrecId);
        }

        public static void HideMrec()
        {
            if (provider != null && config != null) provider.HideMrec(config.GetCurrentPlatformUnits().MrecId);
        }

        public static double GetRetryDelaySeconds(int retryAttempt)
        {
            return Math.Pow(2, Math.Min(6, Math.Max(1, retryAttempt)));
        }

        private static bool IsInterstitialAllowed()
        {
            if (config == null || !config.AdsEnabled || config.Interstitial == null || !config.Interstitial.Enabled) return false;
            var rules = config.Interstitial;
            return Clock() - sessionStartedAt >= rules.MinimumSessionTime
                && Clock() - lastInterstitialAt >= rules.CooldownSeconds
                && completedLevels >= Math.Max(1, rules.LevelInterval);
        }

        internal sealed class Once
        {
            private Action action;
            public Once(Action action) { this.action = action; }
            public void Invoke() { var callback = action; action = null; callback?.Invoke(); }
        }

        internal sealed class Once<T>
        {
            private Action<T> action;
            public Once(Action<T> action) { this.action = action; }
            public void Invoke(T value) { var callback = action; action = null; callback?.Invoke(value); }
        }
    }
}
