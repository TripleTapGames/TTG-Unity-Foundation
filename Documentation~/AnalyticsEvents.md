# Analytics events guide

TTG Foundation provides one game-facing analytics API and forwards explicit calls to every enabled, initialized provider. It does not automatically emit gameplay, retention, purchase, or ad-revenue events. Put game-specific timing and event decisions in scripts under the consuming project's `Assets` folder.

## Requirements

Before sending an event:

1. Initialize through `TTGBootstrap` or `TTGInitializer.InitializeAsync`.
2. Enable and configure the intended providers in `TTGProjectConfig`.
3. Apply resolved analytics consent through `TTGPrivacy.SetConsentAsync`.
4. Wait until initialization and deferred consent-dependent initialization finish.

The facade does not queue events sent before a provider is ready. Singular native delivery and paid ad callbacks must be verified on Android or iOS; a quiet Unity Console does not prove that an event reached a dashboard.

## Copy-ready game analytics script

Create `Assets/Game/Scripts/Analytics/GameAnalyticsEvents.cs` in the game project:

```csharp
using System;
using System.Collections.Generic;
using TripleTapGames.Foundation;
using UnityEngine;

namespace TripleTapGames.Game.Analytics
{
    public static class GameAnalyticsEvents
    {
        private static readonly Dictionary<int, double> LevelStartTimes =
            new Dictionary<int, double>();

        public static void LevelStarted(int levelNumber)
        {
            if (!CanSend()) return;
            LevelStartTimes[levelNumber] = Time.realtimeSinceStartupAsDouble;
            TTGAnalytics.LevelStarted(LevelId(levelNumber));
        }

        public static void LevelCompleted(int levelNumber)
        {
            if (!CanSend()) return;
            TTGAnalytics.LevelCompleted(LevelId(levelNumber));
            SendLevelDuration(levelNumber, "complete");
            TTGAnalytics.MilestoneCompleted(levelNumber);
        }

        public static void LevelFailed(int levelNumber)
        {
            if (!CanSend()) return;
            TTGAnalytics.LevelFailed(LevelId(levelNumber));
            SendLevelDuration(levelNumber, "failed");
        }

        public static void DesignEvent(string eventName,
            IReadOnlyDictionary<string, object> parameters = null)
        {
            if (!CanSend()) return;
            TTGAnalytics.LogEvent(eventName, parameters);
        }

        public static void RecordRetention()
        {
            if (!CanSend()) return;
            TTGAnalytics.RecordSessionRetention();
        }

        public static void ConfirmedPurchase(TTGPurchaseResult result)
        {
            if (result == null || result.Status != TTGPurchaseStatus.Succeeded || !CanSend()) return;
            TTGAnalytics.Purchase(result.ProductId, result.LocalizedPrice,
                result.Currency, result.TransactionId);
        }

        public static void AdRevenue(string networkName, string adFormat,
            string placement, string adUnitId, double revenue)
        {
            if (!CanSend()) return;
            TTGAnalytics.AdImpression(new TTGAdImpression
            {
                AdSource = "AppLovin",
                NetworkName = networkName,
                AdFormat = adFormat,
                Placement = placement,
                AdUnitId = adUnitId,
                Currency = "USD",
                Revenue = revenue
            });
        }

        private static void SendLevelDuration(int levelNumber, string result)
        {
            if (!LevelStartTimes.TryGetValue(levelNumber, out var startedAt)) return;
            LevelStartTimes.Remove(levelNumber);
            var duration = Math.Max(0, Time.realtimeSinceStartupAsDouble - startedAt);
            TTGAnalytics.LogEvent("level_duration", new Dictionary<string, object>
            {
                { "level_id", LevelId(levelNumber) },
                { "result", result },
                { "duration_seconds", duration }
            });
        }

        private static bool CanSend()
        {
            return TTGInitializer.IsInitialized
                && TTGPrivacy.State.Analytics == TTGConsentStatus.Granted;
        }

        private static string LevelId(int levelNumber) => "level_" + levelNumber;
    }
}
```

If the game can run more than one level concurrently, use a unique attempt ID rather than only the level number as the timer key.

## Custom and design events

Use lowercase snake-case names and only parameters needed for analysis:

```csharp
GameAnalyticsEvents.DesignEvent("tutorial_completed",
    new Dictionary<string, object>
    {
        { "step_count", 5 },
        { "duration_seconds", 42.3 }
    });
```

GameAnalytics maps non-progression calls to design events. Firebase, Facebook, and Singular receive their adapter's normalized custom-event representation.

## Level progression and duration

Call start exactly once when interactive gameplay begins:

```csharp
GameAnalyticsEvents.LevelStarted(levelNumber);
```

Call exactly one outcome:

```csharp
GameAnalyticsEvents.LevelCompleted(levelNumber);
// or
GameAnalyticsEvents.LevelFailed(levelNumber);
```

This produces `level_start`, `level_complete` or `level_fail`, plus a separate `level_duration` event containing `level_id`, `result`, and `duration_seconds`. The separate duration event preserves GameAnalytics native progression mapping while giving every provider duration data.

Foundation does not own or observe the game's level lifecycle. Call these methods beside the game's own start, win, or lose transition, and guard each gameplay outcome so it executes once.

## Milestones

`TTGAnalytics.MilestoneCompleted` recognizes levels 5, 10, 15, and 20 and sends these events once per local installation:

```text
level_5_completed
level_10_completed
level_15_completed
level_20_completed
```

For another milestone, send an explicit custom event:

```csharp
GameAnalyticsEvents.DesignEvent("level_25_completed");
```

Milestone state is stored in `PlayerPrefs`. Clearing application data resets it.

## Retention

After TTG initialization and consent complete, call once for the session:

```csharp
GameAnalyticsEvents.RecordRetention();
```

The helper is idempotent for the current UTC day and produces `day_0_retention`, then `day_N_retention` on later UTC return days. Its first-login and last-sent markers are stored in `PlayerPrefs`.

## Confirmed in-app purchases

Never send a purchase event when the button is pressed. Send it only after Unity IAP returns `Succeeded`:

```csharp
TTGIAP.Purchase(productId, result =>
{
    if (result.Status != TTGPurchaseStatus.Succeeded) return;

    GameAnalyticsEvents.ConfirmedPurchase(result);
    GrantAndPersistEntitlement(result.ProductId);
});
```

The normalized event contains `product_id`, `price`, `currency`, and `transaction_id`. In Foundation v0.2.1 it remains a normalized custom purchase event rather than a native revenue call for every vendor. Backend receipt validation remains the game's responsibility.

## AppLovin ad revenue

The TTG Unity Template contains `Assets/Game/Scripts/Analytics/AppLovinAdRevenueAnalytics.cs`. It automatically creates one persistent game-owned bridge and subscribes to MAX paid-impression callbacks for interstitial, rewarded, banner, MREC, and app-open ads.

MAX invokes paid callbacks on a background thread. The bridge copies the impression values into a thread-safe queue and calls `TTGAnalytics.AdImpression` from Unity's main thread. Do not add another callback that reports the same impression.

For another ad integration, call `GameAnalyticsEvents.AdRevenue` from that SDK's confirmed paid-impression callback. Pass actual revenue from the callback; never invent or estimate it.

Singular maps this event to its native `SingularSDK.AdRevenue` API. Firebase, Facebook, and GameAnalytics currently receive normalized event representations.

## Provider behavior

| TTG call | GameAnalytics | Firebase | Singular | Facebook |
| --- | --- | --- | --- | --- |
| `LogEvent` | Design event | Custom event | Custom event | App event |
| Level start/complete/fail | Native progression | Custom event | Custom event | App event |
| `MilestoneCompleted` | Design event | Custom event | Custom event | App event |
| `RecordSessionRetention` | Design event | Custom event | Custom event | App event |
| `Purchase` | Design event | Normalized purchase event | Normalized purchase event | Normalized app event |
| `AdImpression` | Design event | Normalized ad-impression event | Native ad revenue | Normalized app event |

Only enabled, initialized providers receive an event. One provider throwing an exception does not stop the others.

## Verification checklist

- Start from Loading so initialization and consent run before gameplay.
- Confirm the provider is enabled and Project Setup validation passes.
- Use a development account and test devices, never live ad clicking.
- Verify Firebase configuration belongs to the current bundle/application ID.
- Verify Singular on Android or iOS rather than relying on Editor behavior.
- Use each vendor's debug/device dashboard to confirm delivery.
- Confirm each gameplay outcome, purchase, and paid impression is reported once.
- Clear `PlayerPrefs` only when intentionally retesting retention or milestone first-time behavior.
