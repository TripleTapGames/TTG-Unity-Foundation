using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using UnityEngine;

namespace TripleTapGames.Foundation.Adapters.Firebase
{
    internal sealed class FirebaseService : ITTGService, ITTGAnalyticsProvider, ITTGConsentAdapter, ITTGCrashReporter
    {
        private bool analyticsEnabled;
        private bool crashlyticsEnabled;

        public string ServiceName => "Firebase";
        public string ProviderName => "Firebase Analytics";
        public int InitializationOrder => 200;
        public bool IsInitialized { get; private set; }
        public bool RequiresConsent => true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => TTGServiceRegistry.Global.Register(new FirebaseService());

        public bool IsEnabled(TTGProjectConfig config) => config.Firebase.Enabled;
        public bool IsRequired(TTGProjectConfig config) => config.Firebase.Required;

        public async UniTask<TTGInitializationResult> InitializeAsync(TTGServiceContext context, CancellationToken cancellationToken)
        {
            if (IsInitialized) return TTGInitializationResult.Successful(ServiceName);
            analyticsEnabled = context.ProjectConfig.Firebase.AnalyticsEnabled;
            crashlyticsEnabled = context.ProjectConfig.Firebase.CrashlyticsEnabled;
#if UNITY_EDITOR
            var streamingAssets = Path.Combine(Application.dataPath, "StreamingAssets");
            if (!File.Exists(Path.Combine(streamingAssets, "google-services-desktop.json"))
                && !File.Exists(Path.Combine(streamingAssets, "google-services.json")))
                return new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure,
                    "Firebase Editor options are missing. Add this game's Firebase configuration file, let Firebase generate StreamingAssets options, then try again.");
#endif
            var status = await FirebaseApp.CheckAndFixDependenciesAsync();
            cancellationToken.ThrowIfCancellationRequested();
            if (status != DependencyStatus.Available)
                return new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "Firebase dependencies are unavailable: " + status);

            _ = FirebaseApp.DefaultInstance;
            IsInitialized = true;
            if (analyticsEnabled) TTGAnalytics.RegisterProvider(this);
            if (crashlyticsEnabled)
            {
                Crashlytics.IsCrashlyticsCollectionEnabled = true;
                TTGCrashReporting.Configure(this);
            }
            ApplyConsent(TTGPrivacy.State);
            return TTGInitializationResult.Successful(ServiceName);
        }

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!analyticsEnabled) return;
            if (parameters == null || parameters.Count == 0) { FirebaseAnalytics.LogEvent(eventName); return; }
            var converted = new List<Parameter>();
            foreach (var item in parameters)
            {
                if (item.Value is int intValue) converted.Add(new Parameter(item.Key, intValue));
                else if (item.Value is long longValue) converted.Add(new Parameter(item.Key, longValue));
                else if (item.Value is float floatValue) converted.Add(new Parameter(item.Key, floatValue));
                else if (item.Value is double doubleValue) converted.Add(new Parameter(item.Key, doubleValue));
                else converted.Add(new Parameter(item.Key, item.Value?.ToString() ?? string.Empty));
            }
            FirebaseAnalytics.LogEvent(eventName, converted.ToArray());
        }

        public UniTask ApplyConsentAsync(TTGConsentState state, CancellationToken cancellationToken)
        {
            ApplyConsent(state);
            return UniTask.CompletedTask;
        }

        private static void ApplyConsent(TTGConsentState state)
        {
            FirebaseAnalytics.SetAnalyticsCollectionEnabled(state.Analytics == TTGConsentStatus.Granted);
        }

        public void Shutdown()
        {
            TTGAnalytics.UnregisterProvider(this);
            IsInitialized = false;
        }

        public void RecordException(Exception exception)
        {
            if (crashlyticsEnabled) Crashlytics.LogException(exception);
        }
    }
}
