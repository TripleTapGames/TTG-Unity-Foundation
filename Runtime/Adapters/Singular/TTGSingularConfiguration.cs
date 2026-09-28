using System;
using Singular;
using UnityEngine;

namespace TripleTapGames.Foundation.Adapters.Singular
{
    internal static class TTGSingularConfiguration
    {
        internal static void Apply(TTGSingularConfig config, SingularSDK sdk)
        {
            if (config == null || sdk == null) throw new InvalidOperationException("Singular configuration or component is missing.");
            sdk.SingularAPIKey = config.ApiKey ?? "";
            sdk.SingularAPISecret = config.ApiSecret ?? "";
            sdk.InitializeOnAwake = false; // TTG owns initialization after consent.
            sdk.enableLogging = false; // Vendor debug logs include credentials.
            sdk.logLevel = Mathf.Clamp(config.LogLevel, 0, 3);
            sdk.SKANEnabled = config.SkanEnabled;
            sdk.waitForTrackingAuthorizationWithTimeoutInterval = config.WaitForTrackingAuthorization
                ? Math.Max(0, config.TrackingAuthorizationTimeout) : 0;
            sdk.enableODMWithTimeoutInterval = config.OdmEnabled ? config.OdmTimeout : -1;
            // Do not set facebookAppId: it is not a Singular credential.
        }
    }
}
