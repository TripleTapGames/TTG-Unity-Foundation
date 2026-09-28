using System.Threading;
using Cysharp.Threading.Tasks;
using Singular;
using UnityEngine;

namespace TripleTapGames.Foundation.Adapters.Singular
{
    internal sealed class SingularService : ITTGService, ITTGConsentAdapter
    {
        private GameObject sdkObject;
        public string ServiceName => "Singular";
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
            var instances = Object.FindObjectsOfType<SingularSDK>(true);
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
            if (sdkObject.transform.parent == null) Object.DontDestroyOnLoad(sdkObject);
            if (Application.isEditor)
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Warning,
                    "Singular settings applied; native initialization must be verified on a mobile device."));
            SingularSDK.InitializeSingularSDK();
            IsInitialized = SingularSDK.Initialized;
            if (!IsInitialized)
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "Singular did not initialize."));
            ApplyConsentAsync(TTGPrivacy.State, cancellationToken).Forget();
            return UniTask.FromResult(TTGInitializationResult.Successful(ServiceName));
        }

        public UniTask ApplyConsentAsync(TTGConsentState state, CancellationToken cancellationToken)
        {
            if (state.Analytics == TTGConsentStatus.Denied) SingularSDK.StopAllTracking();
            else if (state.Analytics == TTGConsentStatus.Granted) SingularSDK.ResumeAllTracking();
            return UniTask.CompletedTask;
        }

        public void Shutdown() => IsInitialized = false;
    }
}
