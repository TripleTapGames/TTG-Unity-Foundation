# Legacy migration bridge

This sample is intentionally documentation-only. Importing duplicate `TripleTapSDK` classes while the originals remain would break compilation and serialized references.

For a game migration:

1. Preserve each legacy script's `.meta` GUID.
2. Replace its implementation in `Assets` with an `[Obsolete]` forwarding component that calls `TTGInitializer`, `TTGAnalytics`, `TTGAds`, or `TTGIAP`.
3. Open and resave affected scenes and prefabs.
4. Move gameplay callers to TTG Foundation facades.
5. Remove the bridge only after the project contains no legacy serialized references.

The Pop Sort host is not migrated by package installation.
