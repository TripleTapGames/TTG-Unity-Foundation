using System;
using System.Threading;
using AppLovinMax;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TripleTapGames.Foundation.Adapters.AppLovin
{
    internal sealed class AppLovinAdsService : ITTGService, ITTGAdsProvider, ITTGConsentAdapter
    {
        private UniTaskCompletionSource<TTGInitializationResult> initialization;
        private TTAdsConfig adsConfig;
        private Action<TTGAdResult> interstitialCompleted;
        private Action rewarded;
        private Action<TTGAdResult> rewardedClosed;
        private int interstitialRetries;
        private int rewardedRetries;

        public string ServiceName => "AppLovin MAX";
        public int InitializationOrder => 500;
        public bool IsInitialized { get; private set; }
        public bool RequiresConsent => true;
        public bool IsInterstitialReady => IsInitialized && MaxSdk.IsInterstitialReady(Units.InterstitialId);
        public bool IsRewardedReady => IsInitialized && MaxSdk.IsRewardedAdReady(Units.RewardedId);
        private TTGPlatformAdUnits Units => adsConfig.GetCurrentPlatformUnits();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            TTGServiceRegistry.Global.Register(new AppLovinAdsService());
        }

        public bool IsEnabled(TTGProjectConfig config) => config.AppLovin.Enabled;
        public bool IsRequired(TTGProjectConfig config) => config.AppLovin.Required;

        public UniTask<TTGInitializationResult> InitializeAsync(TTGServiceContext context, CancellationToken cancellationToken)
        {
            if (IsInitialized) return UniTask.FromResult(TTGInitializationResult.Successful(ServiceName));
            if (initialization != null) return initialization.Task;
            if (context.AdsConfig == null || !context.AdsConfig.AdsEnabled)
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "TTAdsConfig is missing or ads are disabled."));
            if (string.IsNullOrWhiteSpace(context.ProjectConfig.AppLovin.SdkKey))
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "The MAX SDK key is missing."));

            adsConfig = context.AdsConfig;
            initialization = new UniTaskCompletionSource<TTGInitializationResult>();
            Subscribe();
            ApplyConsent(TTGPrivacy.State);
            MaxSdkCallbacks.OnSdkInitializedEvent += OnSdkInitialized;
            MaxSdk.SetSdkKey(context.ProjectConfig.AppLovin.SdkKey);
            MaxSdk.InitializeSdk();
            cancellationToken.Register(() => initialization.TrySetCanceled(cancellationToken));
            return initialization.Task;
        }

        private void OnSdkInitialized(MaxSdkBase.SdkConfiguration configuration)
        {
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            IsInitialized = true;
            TTGAds.Configure(this, adsConfig);
            LoadInterstitial();
            LoadRewarded();
            initialization.TrySetResult(TTGInitializationResult.Successful(ServiceName));
        }

        public void ShowInterstitial(string adUnitId, TTGAdPlacement placement, Action<TTGAdResult> completed)
        {
            interstitialCompleted = completed;
            MaxSdk.ShowInterstitial(adUnitId, placement.ToString());
        }

        public void ShowRewarded(string adUnitId, TTGAdPlacement placement, Action onRewarded, Action<TTGAdResult> closed)
        {
            rewarded = onRewarded;
            rewardedClosed = closed;
            MaxSdk.ShowRewardedAd(adUnitId, placement.ToString());
        }

        public void ShowBanner(string adUnitId, TTGBannerPosition position)
        {
            var maxPosition = position == TTGBannerPosition.Top ? MaxSdk.AdViewPosition.TopCenter : MaxSdk.AdViewPosition.BottomCenter;
            MaxSdk.CreateBanner(adUnitId, new MaxSdk.AdViewConfiguration(maxPosition));
            MaxSdk.ShowBanner(adUnitId);
        }

        public void HideBanner(string adUnitId) => MaxSdk.HideBanner(adUnitId);

        public void ShowMrec(string adUnitId)
        {
            MaxSdk.CreateMRec(adUnitId, new MaxSdk.AdViewConfiguration(MaxSdk.AdViewPosition.Centered));
            MaxSdk.ShowMRec(adUnitId);
        }

        public void HideMrec(string adUnitId) => MaxSdk.HideMRec(adUnitId);

        public UniTask ApplyConsentAsync(TTGConsentState state, CancellationToken cancellationToken)
        {
            ApplyConsent(state);
            return UniTask.CompletedTask;
        }

        private static void ApplyConsent(TTGConsentState state)
        {
            MaxSdk.SetHasUserConsent(state.Advertising == TTGConsentStatus.Granted);
            MaxSdk.SetDoNotSell(state.AdPersonalization == TTGConsentStatus.Denied);
        }

        private void Subscribe()
        {
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHidden;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnRevenuePaid;
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardReceived;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedHidden;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRevenuePaid;
        }

        private void Unsubscribe()
        {
            MaxSdkCallbacks.OnSdkInitializedEvent -= OnSdkInitialized;
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= OnInterstitialLoaded;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= OnInterstitialLoadFailed;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= OnInterstitialHidden;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnInterstitialDisplayFailed;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= OnRevenuePaid;
            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= OnRewardedLoaded;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= OnRewardedLoadFailed;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnRewardReceived;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnRewardedHidden;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnRewardedDisplayFailed;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= OnRevenuePaid;
        }

        private void LoadInterstitial() { if (!string.IsNullOrWhiteSpace(Units.InterstitialId)) MaxSdk.LoadInterstitial(Units.InterstitialId); }
        private void LoadRewarded() { if (!string.IsNullOrWhiteSpace(Units.RewardedId)) MaxSdk.LoadRewardedAd(Units.RewardedId); }
        private void OnInterstitialLoaded(string id, MaxSdkBase.AdInfo info) => interstitialRetries = 0;
        private void OnRewardedLoaded(string id, MaxSdkBase.AdInfo info) => rewardedRetries = 0;
        private void OnInterstitialLoadFailed(string id, MaxSdkBase.ErrorInfo error) => RetryInterstitialAsync(++interstitialRetries).Forget();
        private void OnRewardedLoadFailed(string id, MaxSdkBase.ErrorInfo error) => RetryRewardedAsync(++rewardedRetries).Forget();

        private async UniTaskVoid RetryInterstitialAsync(int attempt)
        {
            if (attempt > 8) return;
            await UniTask.Delay(TimeSpan.FromSeconds(TTGAds.GetRetryDelaySeconds(attempt)), ignoreTimeScale: true);
            LoadInterstitial();
        }

        private async UniTaskVoid RetryRewardedAsync(int attempt)
        {
            if (attempt > 8) return;
            await UniTask.Delay(TimeSpan.FromSeconds(TTGAds.GetRetryDelaySeconds(attempt)), ignoreTimeScale: true);
            LoadRewarded();
        }

        private void OnInterstitialHidden(string id, MaxSdkBase.AdInfo info)
        {
            var callback = interstitialCompleted; interstitialCompleted = null;
            callback?.Invoke(new TTGAdResult(TTGAdResultStatus.Completed));
            LoadInterstitial();
        }

        private void OnInterstitialDisplayFailed(string id, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo info)
        {
            var callback = interstitialCompleted; interstitialCompleted = null;
            callback?.Invoke(new TTGAdResult(TTGAdResultStatus.Failed, error.Message));
            LoadInterstitial();
        }

        private void OnRewardReceived(string id, MaxSdk.Reward reward, MaxSdkBase.AdInfo info) => rewarded?.Invoke();

        private void OnRewardedHidden(string id, MaxSdkBase.AdInfo info)
        {
            rewarded = null;
            var callback = rewardedClosed; rewardedClosed = null;
            callback?.Invoke(new TTGAdResult(TTGAdResultStatus.Closed));
            LoadRewarded();
        }

        private void OnRewardedDisplayFailed(string id, MaxSdkBase.ErrorInfo error, MaxSdkBase.AdInfo info)
        {
            rewarded = null;
            var callback = rewardedClosed; rewardedClosed = null;
            callback?.Invoke(new TTGAdResult(TTGAdResultStatus.Failed, error.Message));
            LoadRewarded();
        }

        private static void OnRevenuePaid(string id, MaxSdkBase.AdInfo info)
        {
            TTGAnalytics.AdImpression(new TTGAdImpression
            {
                AdSource = "AppLovin",
                NetworkName = info.NetworkName,
                AdFormat = info.AdFormat,
                Placement = info.Placement,
                AdUnitId = id,
                Revenue = info.Revenue
            });
        }

        public void Shutdown()
        {
            Unsubscribe();
            IsInitialized = false;
            initialization = null;
        }
    }
}
