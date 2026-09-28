using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TripleTapGames.Foundation
{
    public enum TTGConsentStatus
    {
        Unknown,
        Granted,
        Denied,
        NotApplicable
    }

    public enum TTGTrackingAuthorizationStatus
    {
        Unknown,
        NotDetermined,
        Restricted,
        Denied,
        Authorized,
        NotApplicable
    }

    [Serializable]
    public sealed class TTGConsentState
    {
        public TTGConsentStatus Analytics = TTGConsentStatus.Unknown;
        public TTGConsentStatus Advertising = TTGConsentStatus.Unknown;
        public TTGConsentStatus AdPersonalization = TTGConsentStatus.Unknown;
        public TTGConsentStatus AgeRestricted = TTGConsentStatus.Unknown;
        public TTGTrackingAuthorizationStatus Tracking = TTGTrackingAuthorizationStatus.Unknown;

        public bool IsResolved => Analytics != TTGConsentStatus.Unknown && Advertising != TTGConsentStatus.Unknown;
    }

    public interface ITTGConsentAdapter
    {
        UniTask ApplyConsentAsync(TTGConsentState state, CancellationToken cancellationToken);
    }

    public static class TTGPrivacy
    {
        private const string PlayerPrefsKey = "TTG.Foundation.Consent.v1";
        private static TTGConsentState state = new TTGConsentState();

        public static TTGConsentState State => state;
        public static bool HasResolvedConsent => state != null && state.IsResolved;
        public static event Action<TTGConsentState> OnConsentChanged;

        public static void LoadPersistedState()
        {
            if (!PlayerPrefs.HasKey(PlayerPrefsKey)) return;
            try
            {
                var loaded = JsonUtility.FromJson<TTGConsentState>(PlayerPrefs.GetString(PlayerPrefsKey));
                if (loaded != null) state = loaded;
            }
            catch (Exception exception)
            {
                TTGLogger.Warning(TTGLogCategory.Privacy, "Stored consent could not be read: " + exception.GetType().Name);
            }
        }

        public static async UniTask<IReadOnlyList<TTGInitializationResult>> SetConsentAsync(
            TTGConsentState newState,
            CancellationToken cancellationToken = default)
        {
            if (newState == null) throw new ArgumentNullException(nameof(newState));
            state = newState;
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(state));
            PlayerPrefs.Save();

            foreach (var service in TTGServiceRegistry.Global.Services)
            {
                // Some vendor consent APIs construct or access their native singleton.
                // Before initialization we only store the normalized TTG state; each
                // adapter applies that state as part of its own initialization.
                if (service.IsInitialized && service is ITTGConsentAdapter adapter)
                {
                    try
                    {
                        await adapter.ApplyConsentAsync(state, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        TTGLogger.Error(TTGLogCategory.Privacy, "A consent adapter failed.", exception);
                    }
                }
            }

            OnConsentChanged?.Invoke(state);
            return await TTGInitializer.InitializeDeferredServicesAsync(cancellationToken);
        }

        internal static void ResetForTests()
        {
            state = new TTGConsentState();
            PlayerPrefs.DeleteKey(PlayerPrefsKey);
        }
    }
}
