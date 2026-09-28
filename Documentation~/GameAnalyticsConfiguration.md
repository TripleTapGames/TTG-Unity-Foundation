# GameAnalytics configuration

Tested with GameAnalytics 8.2.0 via OpenUPM.

1. Stop Play mode. Open Tools > Triple Tap Games > Project Setup.
2. Create / Locate TTG Project Config. Enable GameAnalytics and enter separate Android and iOS game/secret keys. Leave an unused platform blank; the active mobile platform must have a complete pair before initialization.
3. Click Apply Configuration. The GameAnalytics editor adapter creates or locates `Assets/Resources/GameAnalytics/Settings.asset`, updates Android and iPhonePlayer entries with the vendor's public settings APIs, and saves it. Open that asset to inspect the entries. No key values are logged.
4. Restart Play mode/device startup. Runtime initialization also applies TTG values before calling GameAnalytics.Initialize. Already-initialized SDK instances cannot have credentials replaced: remove duplicate GA startup components and restart instead.

TTG config is the source of truth for Android and iOS keys. Manual vendor-asset key edits are overwritten on the next Apply. Blank TTG values clear stale mobile credentials. Other platforms, build settings and unrelated options are preserved. Repeated Apply does not duplicate platform rows. Duplicate credentials across platforms are rejected before changing keys, matching the vendor's uniqueness restrictions.

The vendor Settings.asset and its metadata are ignored by Git because they contain game credentials. Each developer/CI job must provision TTG config and Apply Configuration before a build; do not commit populated settings. Previously tracked assets need a separately reviewed index-only untracking step.

This change configures keys; it does not change event mappings or implement Singular events. Editor Play mode is not proof of analytics delivery: verify startup, consent and events on a supported mobile device.
