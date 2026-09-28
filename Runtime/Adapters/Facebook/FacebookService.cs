using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Facebook.Unity;
using UnityEngine;

namespace TripleTapGames.Foundation.Adapters.Facebook
{
    internal sealed class FacebookService : ITTGService, ITTGAnalyticsProvider, ITTGConsentAdapter
    {
        private UniTaskCompletionSource<TTGInitializationResult> initialization;
        public string ServiceName => "Facebook";
        public string ProviderName => "Facebook Analytics";
        public int InitializationOrder => 300;
        public bool IsInitialized { get; private set; }
        public bool RequiresConsent => true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register() => TTGServiceRegistry.Global.Register(new FacebookService());

        public bool IsEnabled(TTGProjectConfig config) => config.Facebook.Enabled;
        public bool IsRequired(TTGProjectConfig config) => config.Facebook.Required;

        public UniTask<TTGInitializationResult> InitializeAsync(TTGServiceContext context, CancellationToken cancellationToken)
        {
            if (IsInitialized) return UniTask.FromResult(TTGInitializationResult.Successful(ServiceName));
            if (initialization != null) return initialization.Task;
            if (string.IsNullOrWhiteSpace(context.ProjectConfig.Facebook.AppId))
                return UniTask.FromResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "Facebook App ID is missing."));

            initialization = new UniTaskCompletionSource<TTGInitializationResult>();
            cancellationToken.Register(() => initialization.TrySetCanceled(cancellationToken));
            if (FB.IsInitialized) CompleteInitialization();
            else FB.Init(CompleteInitialization, OnHideUnity);
            return initialization.Task;
        }

        private void CompleteInitialization()
        {
            if (!FB.IsInitialized)
            {
                initialization.TrySetResult(new TTGInitializationResult(ServiceName, TTGInitializationStatus.Failure, "Facebook SDK initialization failed."));
                return;
            }
            FB.ActivateApp();
            IsInitialized = true;
            FB.Mobile.SetAdvertiserTrackingEnabled(
                TTGPrivacy.State.Tracking == TTGTrackingAuthorizationStatus.Authorized);
            TTGAnalytics.RegisterProvider(this);
            initialization.TrySetResult(TTGInitializationResult.Successful(ServiceName));
        }

        private static void OnHideUnity(bool gameShown) { }

        public void LogEvent(string eventName, IReadOnlyDictionary<string, object> parameters = null)
        {
            Dictionary<string, object> mutable = null;
            if (parameters != null) mutable = new Dictionary<string, object>(parameters);
            FB.LogAppEvent(eventName, null, mutable);
        }

        public UniTask ApplyConsentAsync(TTGConsentState state, CancellationToken cancellationToken)
        {
            FB.Mobile.SetAdvertiserTrackingEnabled(state.Tracking == TTGTrackingAuthorizationStatus.Authorized);
            return UniTask.CompletedTask;
        }

        public void Shutdown()
        {
            TTGAnalytics.UnregisterProvider(this);
            IsInitialized = false;
            initialization = null;
        }
    }
}
