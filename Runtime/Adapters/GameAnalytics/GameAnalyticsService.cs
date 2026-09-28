using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameAnalyticsSDK;
using UnityEngine;

namespace TripleTapGames.Foundation.Adapters.GameAnalytics
{
    internal sealed class GameAnalyticsService : ITTGService, ITTGAnalyticsProvider, ITTGConsentAdapter
    {
        public string ServiceName => "GameAnalytics";
        public string ProviderName => "GameAnalytics";
        public int InitializationOrder => 400;
        public bool IsInitialized { get; private set; }
        public bool RequiresConsent => true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => TTGServiceRegistry.Global.Register(new GameAnalyticsService());

        public bool IsEnabled(TTGProjectConfig config) => config.GameAnalytics.Enabled;
        public bool IsRequired(TTGProjectConfig config) => config.GameAnalytics.Required;

        public UniTask<TTGInitializationResult> InitializeAsync(TTGServiceContext context, CancellationToken cancellationToken)
        {
            if (IsInitialized) return UniTask.FromResult(TTGInitializationResult.Successful(ServiceName));
            var platform = context.ProjectConfig.GameAnalytics;
            var key = Application.platform == RuntimePlatform.IPhonePlayer ? platform.IosGameKey : platform.AndroidGameKey;
            var secret = Application.platform == RuntimePlatform.IPhonePlayer ? platform.IosSecretKey : platform.AndroidSecretKey;
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "Platform credentials are missing."));
            if (GameAnalyticsSDK.GameAnalytics.Initialized)
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure,
                    "GameAnalytics was already initialized outside TTG. Remove the duplicate startup path and restart before applying new credentials."));
            TTGGameAnalyticsConfiguration.Apply(platform, GameAnalyticsSDK.GameAnalytics.SettingsGA);
            GameAnalyticsSDK.GameAnalytics.Initialize();
            IsInitialized = true;
            GameAnalyticsSDK.GameAnalytics.SetEnabledEventSubmission(
                TTGPrivacy.State.Analytics == TTGConsentStatus.Granted);
            TTGAnalytics.RegisterProvider(this);
            return UniTask.FromResult(TTGInitializationResult.Successful(ServiceName));
        }

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (TryGetProgression(eventName, parameters, out var progressionStatus, out var levelId))
            {
                GameAnalyticsSDK.GameAnalytics.NewProgressionEvent(progressionStatus, levelId);
                return;
            }
            if (parameters == null) GameAnalyticsSDK.GameAnalytics.NewDesignEvent(eventName);
            else GameAnalyticsSDK.GameAnalytics.NewDesignEvent(eventName, new Dictionary<string, object>(parameters));
        }

        internal static bool TryGetProgression(string eventName, IReadOnlyDictionary<string, object> parameters,
            out GAProgressionStatus status, out string levelId)
        {
            status = GAProgressionStatus.Undefined;
            levelId = string.Empty;
            if (parameters == null || !parameters.TryGetValue("level_id", out var value) || value == null) return false;
            levelId = value.ToString();
            if (string.IsNullOrWhiteSpace(levelId)) return false;
            if (eventName == TTGEventNames.LevelStart) status = GAProgressionStatus.Start;
            else if (eventName == TTGEventNames.LevelComplete) status = GAProgressionStatus.Complete;
            else if (eventName == TTGEventNames.LevelFail) status = GAProgressionStatus.Fail;
            else return false;
            return true;
        }

        public UniTask ApplyConsentAsync(TTGConsentState state, CancellationToken cancellationToken)
        {
            GameAnalyticsSDK.GameAnalytics.SetEnabledEventSubmission(state.Analytics == TTGConsentStatus.Granted);
            return UniTask.CompletedTask;
        }

        public void Shutdown()
        {
            TTGAnalytics.UnregisterProvider(this);
            IsInitialized = false;
        }
    }
}
