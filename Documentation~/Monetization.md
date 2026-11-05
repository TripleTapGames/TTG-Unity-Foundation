# Ads and purchases: 0.3.1 migration

This source revision is not yet a published Git tag. Template embeds the matching package so it can use the fixes before release. Keep production consumers on immutable tags after publishing and validation.

## Android registration and startup

All optional SDK adapter assemblies retain their existing AlwaysLinkAssembly attributes. The generated Firebase adapter must be refreshed through TTG Setup after updating Foundation. AlwaysLinkAssembly lets Unity discover runtime registration; it does not keep every unused method or initialize an SDK twice.

Start through the generated Loading scene. Directly playing a gameplay scene bypasses TTGBootstrap. Do not add a second SDK initializer to compensate. Regenerate Loading through TTG Setup to apply Singular settings; active SingularSDK objects with InitializeOnAwake are rejected during builds. Existing populated config Required flags are never changed by this update.

TTGInitializer.LastReport exposes each service status, message, and exception. TTGBootstrap logs failures even when the service is optional. Required failures remain fatal and the Loading status shows the FailureSummary. Custom loading UIs must reserve enough space or provide a scrollable detail view. Do not show service credentials in custom diagnostic messages.

## Interstitial remote rules

The GameAnalytics adapter installs a reader after initialization. It reads remote values only when analytics consent is granted and remote configs are ready. Otherwise local rules apply. A late-ready remote config affects subsequent checks without resetting session/cooldown clocks.

| Key | Meaning |
| --- | --- |
| AdLevelStart | Minimum completed-level count (at least 1) |
| AdCoolDown | Seconds between interstitial attempts that complete the show callback |
| interstitial_enabled | Boolean enable flag |
| interstitial_min_session_seconds | Seconds since ad service initialization |

Missing, invalid, negative, NaN and infinite values retain the corresponding local value. Neither readiness nor show calls mutate TTAdsConfig. The global AdsEnabled flag remains authoritative. LevelInterval keeps its existing meaning as a minimum completion threshold, not a modulo frequency.

Games call TTGAds.NotifyLevelCompleted(savedCompletedCount) after service initialization to restore the gate and update it after completion. Do not pass the level currently being replayed as permanent progress. Existing FlowGrid-style wrappers that temporarily replace Interstitial rules should be removed when adopting this package. Other remote systems may call TTGAdRemoteConfig.SetReader; a single reader owns this hook.

## Purchases and restored ownership

Unity IAP 4.14.x remains supported; this is not an IAP 5 migration. Its transitive Unity Services Core dependency is initialized before UnityPurchasing. Keep the Unity project linked/configured for Services. Failures remain visible; do not assume every NoProductsAvailable error is a UGS issue.

Product IDs are exact and case-sensitive. An enabled catalog must contain nonempty, unique, whitespace-free IDs. Foundation does not invent a noads product or change existing IDs. Create and activate the matching product/purchase option in the store, configure country availability and license testers, and allow store configuration to propagate. Store-side availability cannot be verified by Editor validation or a fake-store test.

Use TTGIAP.Purchase(id, result => ...) and present result.Message/status in the game's UI. NotInitialized, ProductUnavailable, Cancelled, Failed and Pending should not silently return. Pending can also mean a duplicate local request was rejected while the original request continues; keep the first request's UI active.

Subscribe to TTGIAP.PurchaseReceived before startup to receive transactions delivered without a current purchase-button callback, including restored purchases. Unsubscribe with your object's lifecycle. A successful direct purchase also emits this event: entitlement grants must be idempotent, and games must not grant consumables twice through both callbacks.

TTGIAP.HasReceipt(id) returns null when unavailable/unsupported, false when no receipt is present, and true when the initialized store has a receipt. Use it after startup and after TTGIAP.RestorePurchases completes to reconcile durable non-consumable ownership. The bool restore callback indicates the restore operation's outcome, not whether a particular product is owned. Android returns its inventory during store initialization; iOS uses RestoreTransactions. Receipt presence is not server-side validation, and this change does not implement subscription entitlement validation, refunds or consumable fulfillment.

Foundation owns store access; each game owns its entitlement, no-ads policy, UI, and persistence. The package never globally disables rewarded ads merely because a no-ads item is owned. Test reinstall/restore, cancellations, unavailable products and repeated purchase taps on devices without completing unintended purchases.

## Validation status

New EditMode tests cover remote thresholds and fallback, catalog validation, unavailable-store messages, restored transaction notification and startup failure summaries. Unity MCP compilation/tests and Android/iOS integration must be run before publishing this revision. No store purchase or SDK initialization is proven by source inspection.
