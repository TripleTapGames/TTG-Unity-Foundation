using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TripleTapGames.Foundation
{
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    public sealed class TTGConsentBootstrap : MonoBehaviour
    {
        [Tooltip("Editor testing only. This is never used in a device build.")]
        [SerializeField] private bool grantDevelopmentConsentInEditor = true;

        private void Awake()
        {
#if UNITY_EDITOR
            if (grantDevelopmentConsentInEditor && !TTGPrivacy.HasResolvedConsent)
                ApplyDevelopmentConsentAsync(destroyCancellationToken).Forget();
#endif
        }

        public async UniTask SetConsentAsync(TTGConsentState state, CancellationToken cancellationToken = default)
        {
            await TTGPrivacy.SetConsentAsync(state, cancellationToken);
        }

#if UNITY_EDITOR
        private static async UniTaskVoid ApplyDevelopmentConsentAsync(CancellationToken cancellationToken)
        {
            try
            {
                await TTGPrivacy.SetConsentAsync(new TTGConsentState
                {
                    Analytics = TTGConsentStatus.Granted,
                    Advertising = TTGConsentStatus.Granted,
                    AdPersonalization = TTGConsentStatus.Granted,
                    AgeRestricted = TTGConsentStatus.NotApplicable,
                    Tracking = TTGTrackingAuthorizationStatus.NotApplicable
                }, cancellationToken);
                Debug.Log("[TTG:Privacy] Development consent applied in the Unity Editor only.");
            }
            catch (OperationCanceledException) { }
            catch (Exception exception) { Debug.LogException(exception); }
        }
#endif
    }
}
