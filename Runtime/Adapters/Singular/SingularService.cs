using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Cysharp.Threading.Tasks;
using Singular;
using UnityEngine;

namespace TripleTapGames.Foundation.Adapters.Singular
{
    internal sealed class SingularService : ITTGService, ITTGAnalyticsProvider, ITTGConsentAdapter
    {
        private GameObject sdkObject;
        public string ServiceName => "Singular";
        public string ProviderName => "Singular";
        public int InitializationOrder => 450;
        public bool IsInitialized { get; private set; }
        public bool RequiresConsent => true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => TTGServiceRegistry.Global.Register(new SingularService());

        public bool IsEnabled(TTGProjectConfig config) => config.Singular.Enabled;
        public bool IsRequired(TTGProjectConfig config) => config.Singular.Required;

        public UniTask<TTGInitializationResult> InitializeAsync(TTGServiceContext context, CancellationToken cancellationToken)
        {
            if (IsInitialized) return UniTask.FromResult(TTGInitializationResult.Successful(ServiceName));
            if (string.IsNullOrWhiteSpace(context.ProjectConfig.Singular.ApiKey) || string.IsNullOrWhiteSpace(context.ProjectConfig.Singular.ApiSecret))
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "Singular SDK credentials are missing."));

            if (SingularSDK.Initialized)
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure,
                    "Singular was already initialized outside TTG. Disable automatic SDK startup and restart."));
            var instances = UnityEngine.Object.FindObjectsOfType<SingularSDK>(true);
            if (instances.Length > 1)
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure,
                    "Multiple Singular components found. Keep one SingularSDKObject."));
            SingularSDK sdk;
            if (instances.Length == 0)
            {
                sdkObject = new GameObject("SingularSDKObject");
                sdkObject.SetActive(false);
                sdk = sdkObject.AddComponent<SingularSDK>();
            }
            else
            {
                sdk = instances[0]; sdkObject = sdk.gameObject;
                if (sdkObject.activeInHierarchy && (sdk.enableLogging || sdk.InitializeOnAwake))
                    return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure,
                        "Singular scene object has unsafe automatic startup/logging flags. Apply Configuration outside Play mode and restart."));
            }
            if (sdkObject.transform.parent != null)
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "SingularSDKObject must be a scene root."));
            TTGSingularConfiguration.Apply(context.ProjectConfig.Singular, sdk);
            sdk.enabled = true;
            sdkObject.SetActive(true);
            if (sdkObject.transform.parent == null) UnityEngine.Object.DontDestroyOnLoad(sdkObject);
            if (Application.isEditor)
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Warning,
                    "Singular settings applied; native initialization must be verified on a mobile device."));
            SingularSDK.InitializeSingularSDK();
            IsInitialized = SingularSDK.Initialized;
            if (!IsInitialized)
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "Singular did not initialize."));
            TTGAnalytics.RegisterProvider(this);
            ApplyConsentAsync(TTGPrivacy.State, cancellationToken).Forget();
            return UniTask.FromResult(TTGInitializationResult.Successful(ServiceName));
        }

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            if (eventName == TTGEventNames.AdImpression && TryCreateAdData(parameters, out var adData))
            {
                SingularSDK.AdRevenue(adData);
                return;
            }

            if (parameters == null || parameters.Count == 0) SingularSDK.Event(eventName);
            else SingularSDK.Event(ToSingularParameters(parameters), eventName);
        }

        internal static bool TryCreateAdData(IReadOnlyDictionary<string, object> parameters, out SingularAdData adData)
        {
            adData = null;
            if (parameters == null
                || !TryGetString(parameters, "ad_source", out var source)
                || !TryGetString(parameters, "currency", out var currency)
                || !TryGetDouble(parameters, "revenue", out var revenue)) return false;

            adData = new SingularAdData(source, currency, revenue);
            if (TryGetString(parameters, "network", out var network)) adData.WithNetworkName(network);
            if (TryGetString(parameters, "format", out var format)) adData.WithAdType(format);
            if (TryGetString(parameters, "placement", out var placement)) adData.WithAdPlacmentName(placement);
            if (TryGetString(parameters, "ad_unit_id", out var adUnitId)) adData.WithAdUnitId(adUnitId);
            return adData.HasRequiredParams();
        }

        internal static Dictionary<string, object> ToSingularParameters(IReadOnlyDictionary<string, object> parameters)
        {
            var result = new Dictionary<string, object>();
            if (parameters == null) return result;
            foreach (var item in parameters)
            {
                if (item.Value is decimal decimalValue) result[item.Key] = (double)decimalValue;
                else if (item.Value is bool boolValue) result[item.Key] = boolValue ? 1 : 0;
                else result[item.Key] = item.Value;
            }
            return result;
        }

        private static bool TryGetString(IReadOnlyDictionary<string, object> values, string key, out string value)
        {
            value = null;
            if (!values.TryGetValue(key, out var raw) || raw == null) return false;
            value = Convert.ToString(raw, CultureInfo.InvariantCulture);
            return !string.IsNullOrWhiteSpace(value);
        }

        private static bool TryGetDouble(IReadOnlyDictionary<string, object> values, string key, out double value)
        {
            value = 0;
            if (!values.TryGetValue(key, out var raw) || raw == null) return false;
            try { value = Convert.ToDouble(raw, CultureInfo.InvariantCulture); return true; }
            catch (Exception) { return false; }
        }

        public UniTask ApplyConsentAsync(TTGConsentState state, CancellationToken cancellationToken)
        {
            if (state.Analytics == TTGConsentStatus.Denied) SingularSDK.StopAllTracking();
            else if (state.Analytics == TTGConsentStatus.Granted) SingularSDK.ResumeAllTracking();
            return UniTask.CompletedTask;
        }

        public void Shutdown()
        {
            TTGAnalytics.UnregisterProvider(this);
            IsInitialized = false;
        }
    }
}
