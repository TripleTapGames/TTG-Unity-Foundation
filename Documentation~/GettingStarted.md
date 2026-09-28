# Getting started

1. Add the pinned UniTask and TTG Foundation Git dependencies shown in the package README to `Packages/manifest.json`, then allow Unity Package Manager to resolve them.
2. Open **Tools > Triple Tap Games > Project Setup**.
3. Install missing UPM/Git packages. Firebase is excluded from Git: follow [Firebase installation](FirebaseInstallation.md), import pinned Analytics and Crashlytics locally without a duplicate dependency manager, then click Refresh Firebase Adapter. Follow the separate guided Facebook and DOTween imports if needed.
4. Create the TTG project and ads configs.
5. Populate local values directly or set the supported `TTG_*` environment variables and click **Import Values From Environment**.
6. Click **Apply Configuration**, then **Validate Project**.
7. Obtain consent in the host game, call `TTGPrivacy.SetConsentAsync`, then call `TTGInitializer.InitializeAsync`. Alternatively, use `TTGBootstrap` in a loading scene to initialize and transition to the configured next scene.

Generated config is written below `Assets/TTGGenerated/Resources/TTG`. Its folder-level `.gitignore` excludes populated assets.

## Environment variables

- `TTG_FACEBOOK_APP_ID`
- `TTG_FACEBOOK_CLIENT_TOKEN`
- `TTG_APPLOVIN_SDK_KEY`
- `TTG_SINGULAR_API_KEY`
- `TTG_SINGULAR_API_SECRET`
- `TTG_GA_ANDROID_GAME_KEY`
- `TTG_GA_ANDROID_SECRET`
- `TTG_GA_IOS_GAME_KEY`
- `TTG_GA_IOS_SECRET`
- `TTG_FIREBASE_ANDROID_CONFIG_PATH`
- `TTG_FIREBASE_IOS_CONFIG_PATH`

Runtime API keys are recoverable from a shipped client. Do not place backend administrator credentials in Unity configuration.

## Initialization behavior

Initialization is idempotent. Disabled services are skipped, consent-dependent services are deferred while consent is unknown, optional failures become warnings, and required failures fail the report without preventing diagnostics for later services.

## Updating the package

Change only the release tag in the Foundation manifest URL, allow Unity to update `packages-lock.json`, and run project validation and tests. Do not point production projects at the repository's `main` branch.
