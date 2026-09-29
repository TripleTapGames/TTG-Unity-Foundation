# TTG Foundation user guide

This guide explains how to add TTG Foundation to a new or existing Unity game, configure the supported SDKs, initialize them, and call the runtime APIs. The package targets Unity 2022.3 LTS and newer.

## 1. Install the package

Open the Unity project's `Packages/manifest.json`. Add both entries inside `dependencies`:

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
"com.tripletapgames.foundation": "https://github.com/TripleTapGames/TTG-Unity-Foundation.git#v0.2.0"
```

Keep the comma before or after these entries valid JSON. Save the file and return to Unity. Wait until Package Manager finishes downloading packages and the Console has no compilation errors.

Always use a release tag such as `v0.2.0`. Do not make a production game depend directly on `main` because it can change without warning.

If you cloned `TTG-Unity-Template`, these package entries are already present. Open the project and allow Unity to resolve them.

## 2. Open Project Setup

In Unity, select **Tools > Triple Tap Games > Project Setup**.

The window provides these actions:

- **Install Missing Packages** installs the pinned package-managed SDKs.
- **Create / Locate TTG Project Config** creates and selects the main service configuration.
- **Create / Locate TTG Ads Config** creates and selects the advertising configuration.
- **Import Values From Environment** imports supported CI or local environment values without printing them.
- **Apply Configuration** copies TTG values into vendor settings and refreshes dependency defines for the active target.
- **Validate Project** reports setup errors and warnings for the active build target.
- **Create / Update Local Loading Scene** creates the loading, consent, progress, and startup flow.

Generated configuration is stored below `Assets/TTGGenerated/Resources/TTG`. The tool creates `Assets/TTGGenerated/.gitignore` so populated assets remain local. Verify `git status` before every commit, particularly when adding the package to an older repository.

## 3. Install the SDKs you need

The core package requires only UniTask. Vendor adapters compile only when their supported dependencies are available.

| Integration | Supported setup |
| --- | --- |
| AppLovin MAX | UPM package 8.6.6 |
| GameAnalytics | OpenUPM package 8.2.0 |
| Singular | Official Git package 5.10.1 |
| Unity IAP | Unity package 4.14.0 |
| Firebase | Analytics and Crashlytics 13.13.0 imported manually |
| Facebook | Guided local Facebook SDK 17.0.0 import |
| DOTween | Optional guided import; Foundation runtime does not depend on it |

Do not install a GameAnalytics UPM package while an old `Assets/GameAnalytics` copy remains. Do not mix Firebase asset imports and Firebase UPM packages. For Facebook, download the official `sdk-version-17.0.0` Unity package and do not substitute an 18.x release.

### Firebase

Firebase is deliberately not stored in this Git repository. Download Firebase Unity SDK 13.13.0 from the official archive and import both Analytics and Crashlytics `.unitypackage` files.

During import, deselect the bundled `Assets/ExternalDependencyManager` files when the project already uses the package-managed External Dependency Manager. After import:

1. Open Project Setup.
2. Confirm that Firebase reports version 13.13.0 as verified.
3. Click **Refresh Firebase Adapter** and wait for compilation.
4. Put Android's `google-services.json` at `Assets/google-services.json`.
5. Put iOS's `GoogleService-Info.plist` at `Assets/GoogleService-Info.plist`.
6. Enable Firebase in `TTGProjectConfig` and validate the appropriate target.

See [Firebase Installation](FirebaseInstallation.md) for removal, CI, and error-recovery instructions.

## 4. Configure the project

Select **Create / Locate TTG Project Config**. For every integration:

- `Enabled` controls whether TTG initializes it.
- `Required` controls whether its failure fails the complete startup report.
- An optional service failure becomes a warning and later services continue initializing.
- Keep integrations disabled until their SDK and values are ready.

Choose Development, Staging, or Production and configure the enabled services:

- Facebook: App ID and client token.
- Firebase: Analytics and Crashlytics feature toggles.
- GameAnalytics: separate Android and iOS game keys and secret keys.
- AppLovin: SDK key and privacy-policy URL.
- Singular: API key, API secret, logging, SKAdNetwork, tracking authorization, and ODM settings.
- Unity IAP: one entry for every product ID and its Consumable, NonConsumable, or Subscription type.

Select **Create / Locate TTG Ads Config** and set:

- Android and iOS ad-unit IDs separately.
- Interstitial, rewarded, banner, and MREC enablement.
- Interstitial minimum session time, completed-level interval, and cooldown.
- Test mode while developing.

Click **Apply Configuration** after changing configuration or SDK installation. For Singular, this updates the single `SingularSDKObject` in the generated Loading scene. It never copies the Facebook App ID into Singular.

Runtime keys included in a mobile application can be extracted from the build. They must not be treated as privileged server secrets. Backend administrator credentials must never be placed in Unity.

## 5. Handle privacy consent

The game or its consent-management platform owns the consent UI. TTG stores a normalized result and applies it to initialized services. Analytics and advertising must both be resolved before consent-dependent initialization continues.

Example after the player finishes the consent flow:

```csharp
using Cysharp.Threading.Tasks;
using TripleTapGames.Foundation;

public async UniTask ApplyPlayerConsentAsync()
{
    var consent = new TTGConsentState
    {
        Analytics = TTGConsentStatus.Granted,
        Advertising = TTGConsentStatus.Granted,
        AdPersonalization = TTGConsentStatus.Denied,
        AgeRestricted = TTGConsentStatus.NotApplicable,
        Tracking = TTGTrackingAuthorizationStatus.Authorized
    };

    await TTGPrivacy.SetConsentAsync(consent);
}
```

Use the real result for each player and platform. `TTGConsentBootstrap` can grant development consent in the Unity Editor for testing, but it never grants consent automatically in a device build.

If initialization runs while consent is unknown, consent-dependent services are deferred. Calling `SetConsentAsync` later applies vendor flags and initializes newly eligible services.

## 6. Initialize all SDKs

### Recommended loading-scene flow

Click **Create / Update Local Loading Scene**. The tool creates or repairs:

- `Assets/Scenes/Loading.unity`
- a `TTGBootstrap` component;
- a `TTGConsentBootstrap` component;
- a loading canvas, progress bar, and status text;
- the Singular SDK object when Singular is installed and enabled;
- Build Settings order with Loading first and Core second.

`TTGBootstrap` waits for resolved consent when an enabled service requires it, calls `TTGInitializer.InitializeAsync`, updates progress, and loads `Core` after a successful or warning result. A required-service failure keeps the loading screen visible and writes the reason to the Console.

The loading objects do not need `DontDestroyOnLoad`. TTG facades and service registry are static, and vendor adapters handle their own lifetime requirements. Singular's adapter moves its root SDK object to `DontDestroyOnLoad` during initialization. The loading UI should be destroyed when Core loads.

### Manual initialization

For a custom bootstrap, call:

```csharp
using Cysharp.Threading.Tasks;
using TripleTapGames.Foundation;
using UnityEngine;

public sealed class GameStartup : MonoBehaviour
{
    private async UniTaskVoid Start()
    {
        var report = await TTGInitializer.InitializeAsync(destroyCancellationToken);
        if (!report.Succeeded)
        {
            Debug.LogError("Required TTG service initialization failed.");
            return;
        }

        Debug.Log($"TTG startup result: {report.OverallStatus}");
    }
}
```

Repeated calls reuse the same in-progress or completed initialization. Use `TTGInitializer.IsInitializing`, `IsInitialized`, `CurrentProgress`, `OnProgressChanged`, and `OnInitialized` when building custom UI. Call `TTGInitializer.Shutdown()` only when intentionally shutting down and resetting all TTG services.

## 7. Send analytics events

TTG sends an explicitly requested event to every initialized analytics provider and isolates provider exceptions. The package does not own a level sequence and does not automatically emit analytics from initialization, gameplay, IAP, or ad callbacks. Add the calls in a game-owned script under `Assets` so each game controls its event timing and avoids duplicates.

See [Analytics Events](AnalyticsEvents.md) for a complete copy-ready project script covering custom/design events, level duration, milestones, retention, confirmed purchases, and AppLovin ad revenue.

```csharp
using System.Collections.Generic;
using TripleTapGames.Foundation;

TTGAnalytics.LogEvent("button_pressed", new Dictionary<string, object>
{
    { "button", "daily_reward" },
    { "screen", "home" }
});

TTGAnalytics.LevelStarted("Level_12");
TTGAnalytics.LevelCompleted("Level_12");
TTGAnalytics.LevelFailed("Level_12");

// Call explicitly after startup if this game uses TTG retention events.
TTGAnalytics.RecordSessionRetention();

// Call explicitly after a supported milestone is genuinely completed.
TTGAnalytics.MilestoneCompleted(5);
```

GameAnalytics maps the three level methods to native Start, Complete, and Fail progression events. Firebase and other initialized analytics providers receive the TTG event representation.

Firebase, Facebook, GameAnalytics, and Singular receive normalized custom and level events when their adapters are installed, enabled, initialized, and allowed by consent. Singular translates AppLovin `ad_impression` data into its native `SingularAdData` revenue call instead of sending it as an ordinary custom event.

`RecordSessionRetention` sends `day_0_retention`, followed by `day_N_retention` once per UTC return day. `MilestoneCompleted` supports one-time `level_5_completed`, `level_10_completed`, `level_15_completed`, and `level_20_completed` events. These markers use local `PlayerPrefs`; clearing application data resets them.

Do not call the same event through both TTG and a vendor API, or reporting will be duplicated.

## 8. Show ads

Check readiness and use a meaningful placement:

```csharp
if (TTGAds.IsInterstitialReady)
{
    TTGAds.ShowInterstitial(TTGAdPlacement.LevelComplete, result =>
    {
        Debug.Log($"Interstitial result: {result.Status}");
    });
}

if (TTGAds.IsRewardedReady)
{
    TTGAds.ShowRewarded(
        TTGAdPlacement.ExtraCoins,
        onRewarded: () => GrantCoins(),
        onClosed: result => Debug.Log($"Rewarded result: {result.Status}"));
}

TTGAds.ShowBanner();
TTGAds.HideBanner();
TTGAds.ShowMrec();
TTGAds.HideMrec();
```

Grant the reward only inside `onRewarded`. TTG prevents overlapping requests of the same format and guarantees each wrapper callback runs at most once.

Interstitial rules use completed levels, session time, and cooldown. The game must call `TTGAds.NotifyLevelCompleted(levelNumber)` once after accepting a completed level. This updates local ad gating and does not send an analytics event.

## 9. Configure and use IAP

Add every store product to `TTGProjectConfig.IAP.Products`, using exactly the same IDs and product types configured in Google Play Console and App Store Connect. Enable IAP, apply configuration, and validate.

All configured products are passed to Unity IAP during TTG initialization. A purchase is successful only after the Unity store callback confirms it.

```csharp
TTGIAP.Purchase("remove_ads", result =>
{
    switch (result.Status)
    {
        case TTGPurchaseStatus.Succeeded:
            TTGAnalytics.Purchase(result.ProductId, result.LocalizedPrice, result.Currency, result.TransactionId);
            UnlockRemoveAds();
            break;
        case TTGPurchaseStatus.Cancelled:
            Debug.Log("Player cancelled the purchase.");
            break;
        default:
            Debug.LogError(result.Message);
            break;
    }
});

TTGIAP.RestorePurchases(success =>
{
    Debug.Log($"Restore finished: {success}");
});
```

Backend receipt validation is outside package v1. Add server validation before granting high-value or fraud-sensitive purchases.

## 10. Connect the game's level flow

Foundation deliberately does not provide a level sequence or gameplay controller. Keep the game's existing level data, loaders, save system, Win/Lose UI, Next, and Retry behavior. At the points where that system accepts a start or outcome, call TTG explicitly:

```csharp
TTGAnalytics.LevelStarted(levelId);

// Once on a genuine win:
TTGAnalytics.LevelCompleted(levelId);
TTGAnalytics.MilestoneCompleted(levelNumber);
TTGAds.NotifyLevelCompleted(levelNumber);

// Or once on a genuine loss:
TTGAnalytics.LevelFailed(levelId);
```

Use a stable `levelId` and a one-based `levelNumber`. Guard each outcome against duplicate callbacks. See [Connect Your Game](GameFlow.md) for a complete game-owned bridge and migration instructions for Foundation 0.1.x.

## 11. Validate and build

Before every release:

1. Select the actual Android or iOS build target.
2. Open Project Setup and click **Apply Configuration**.
3. Click **Validate Project** and resolve every error.
4. Run EditMode tests.
5. Resolve Android dependencies when applicable.
6. Make a device build and test consent, analytics delivery, ads, attribution, purchases, restore, and crash reporting.

Pre-build validation blocks enabled integrations only when an error is relevant to the active platform. Disabled optional integrations do not require their SDK credentials.

Foundation patches generated Android manifests without replacing custom manifests. iOS post-build contributors add only their required values and avoid duplicate entries where vendor processors already manage them.

## 12. Upgrade Foundation

Change the tag in `Packages/manifest.json`, for example from `#v0.1.5` to `#v0.2.0`. Let Unity update `Packages/packages-lock.json`, then apply configuration, validate, and run tests. Version 0.2.0 removes the former prefab game-flow API; follow [Connect Your Game](GameFlow.md) before upgrading a project that used it.

Do not edit files inside `Library/PackageCache`; Unity can replace them. Make package changes in the standalone `TTG-Unity-Foundation` repository and publish a new semantic-version tag.

## Common problems

- **Project Setup menu is missing:** wait for compilation and fix the first Console compiler error. Confirm both UniTask and Foundation resolved.
- **Services remain deferred:** provide a consent state in the host consent flow. Editor-only development consent does not run on devices.
- **Startup stays on Loading:** inspect `TTGBootstrap.Report` and Console output. A required integration failed, consent is unresolved, or Core is missing from Build Settings.
- **Firebase is missing or incomplete:** import both Analytics and Crashlytics 13.13.0 and refresh the Firebase adapter.
- **GameAnalytics values do not update:** use distinct Android/iOS keys, click Apply Configuration outside Play mode, and remove duplicate legacy initialization.
- **Singular fails initialization:** keep exactly one root `SingularSDKObject`, then Apply Configuration outside Play mode. TTG controls startup.
- **Ads return BlockedByRules:** inspect minimum session time, level interval, cooldown, and calls to `NotifyLevelCompleted`.
- **IAP returns NotInitialized:** wait for successful TTG initialization and verify that the product is present in TTG config and the store dashboard.

For more focused help, see [Troubleshooting](Troubleshooting.md), [Loading Scene](LoadingScene.md), [Connect Your Game](GameFlow.md), and [GameAnalytics Configuration](GameAnalyticsConfiguration.md).
