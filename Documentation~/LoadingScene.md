# Local Loading scene and Singular

In Project Setup, click **Create / Update Local Loading Scene** outside Play mode. It creates `Assets/Scenes/Loading.unity` when absent, or preserves and updates the existing scene. Save any unsaved Loading edits first. The operation refuses to overwrite dirty open scenes.

Open a saved scene before using the generator; it also preserves unsaved untitled scenes rather than closing them. For CI, provision local configs first, then invoke `TripleTapGames.Foundation.Editor.TTGLoadingSceneSetup.CreateOrUpdateForBatch` with Unity batch mode. This requires at least one saved scene in Build Settings and opens that scene in the batch process before generating Loading.

The scene contains the package TTGBootstrap and one root SingularSDKObject with the installed SingularSDK component. Setup upgrades an imported Bootstrap Example by disabling it and adding the package component on the same object. It also creates a simple TTGLoadingScreen with a progress bar and status label when one does not exist.

TTGBootstrap listens to real per-service progress from TTGInitializer. After the initialization pass succeeds, it loads the configurable next scene (`Core` by default). A required-service failure leaves Loading visible and shows a failure message. Optional failures and consent-deferred services produce warnings but do not block Core: deferred SDKs initialize later after the host submits consent. If `Assets/Scenes/Core.unity` exists, setup places it immediately after Loading in Build Settings while preserving all other entries.

The generated bootstrap object also has TTGConsentBootstrap. In the Unity Editor it grants clearly logged development consent before TTGBootstrap starts, allowing SDK lifecycle testing without a CMP. This editor-only shortcut is compiled out of device behavior. In a device build TTGBootstrap waits at “Waiting for consent...” while consent-dependent integrations are enabled. The host CMP must call `TTGConsentBootstrap.SetConsentAsync(...)` or `TTGPrivacy.SetConsentAsync(...)`; initialization and the Core transition then continue. Never use the Editor development choice as production consent.

**Apply Configuration** now also updates and saves this scene. The Singular object receives API key, API secret, log level, SKAN flag, tracking wait timeout and ODM timeout from TTGProjectConfig. Disabled tracking wait uses zero; disabled ODM uses -1. When ODM is enabled, the configured timeout is passed through, so -1 still has the vendor's disabled behavior. Facebook App ID is not copied or changed.

Initialize On Awake and vendor logging are forced off regardless of the corresponding config toggles: TTG owns consent-aware initialization, and Singular's debug logger includes key values. SingularSDKObject is saved inactive. It is deliberately grey in the Hierarchy; the TTG Singular adapter finds it even when inactive, reapplies current config, then activates/initializes it only when enabled and eligible after consent. It is kept across scene changes. Duplicate Singular components or an independently initialized SDK are reported as errors rather than silently reused.

Both Loading.unity and its metadata are ignored by Git because Apply writes credentials into the scene. This is a local generated scene, not a distributable credential-free asset. Fresh clones must run Create / Update Local Loading Scene (or Apply Configuration) to recreate it and create local TTG configs. If someone has already committed a populated Loading scene, ignoring it does not erase history; review untracking and credential rotation separately.

The generator is shared in the package, but the populated scene is not. Do not force-add it to Git. Changes to this local scene are also not shared automatically: keep reusable loading UI/prefabs free of credentials and version them separately.

Singular native initialization is not performed in the Unity Editor. An Editor warning explains this; verify actual initialization and attribution on a mobile device. This change does not add Singular analytics event forwarding.
