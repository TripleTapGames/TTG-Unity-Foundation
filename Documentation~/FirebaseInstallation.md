# Firebase: separate local installation

Firebase binaries are deliberately excluded from Git. A clone or GitHub source ZIP contains the template and TTG Foundation, but not Firebase or populated game configuration. The original local SDK can remain installed: ignore rules do not delete it.

## First setup on a clone

1. Open with Unity 2022.3.62f3 and allow Package Manager to restore the pinned dependencies. Core and setup tooling work without Firebase. Create blank TTG config assets from Project Setup if needed.
2. Open **Tools > Triple Tap Games > Project Setup**. The Firebase section shows Required and Detected versions separately. Firebase disabled in project config does not block a build just because its SDK is absent.
3. Use **Open Official Firebase Downloads** to visit the [official archive](https://developers.google.com/unity/archive). Select **13.13.0**, not whichever release is latest. Download the Analytics and Crashlytics `.unitypackage` artifacts (or the matching SDK archive containing them).
4. Keep installers outside Assets, for example in the ignored `LocalSDKInstallers/` folder. Reuse the same trusted installers for other games; no new download is needed each time.
5. Import both with **Assets > Import Package > Custom Package**. In each import dialog deselect **Assets/ExternalDependencyManager** and its metadata. This project already installs External Dependency Manager 1.2.189 through UPM. Preserve the remaining Firebase dependencies. Do not install Firebase UPM packages or mix Firebase versions. See [Firebase's official installation guidance](https://firebase.google.com/docs/unity/setup-alternative).
6. Wait for imports/compilation, then click **Refresh Firebase Adapter**. A supported import generates the adapter under ignored `Assets/TTGGenerated/FirebaseAdapter`. Wait for compilation again. Repeated refresh is safe and does not recreate unchanged files.
7. Add this game's `Assets/google-services.json` for Android and/or `Assets/GoogleService-Info.plist` for iOS. Never reuse another game's files. These paths are ignored.
8. Enable Firebase in TTGProjectConfig only when ready, select the build target, and run **Validate Project**. Connect initialization and consent using the main user guide. Test actual native builds and delivery in the Firebase console.

## Status and error handling

- **Missing:** no Assets/Firebase installation.
- **Incomplete:** required managed modules are absent; import both products, including core dependencies.
- **Verified:** vendor release manifests and the desktop core filename match 13.13.0, and managed DLL identities are valid. Firebase assembly version 1.0.0.0 is not its SDK release number. This is metadata verification, not cryptographic provenance or native-build certification.
- **Unverified:** missing/ambiguous manifests or unreadable DLL metadata. Do not manually rename manifests to bypass checks; reinstall trusted artifacts.
- **Mismatch:** product manifests differ from the pin or mixed native versions are present.
- **ConflictingImport:** Firebase UPM packages are present. This template supports only the guided asset-import route.

Enabled Firebase blocks validation for unsupported installs or a missing/outdated local adapter, regardless of which runtime feature toggle is on: the adapter requires Analytics and Crashlytics together. Mobile configuration files are checked only for their own target. An inactive Firebase service does not block builds for these findings; imported broken vendor files can still cause vendor compilation errors independently of TTG.

## Refresh, upgrade or remove

The template does not save `TTG_FIREBASE` in PlayerSettings. Its package adapter source is inactive by default; Refresh creates a local compilable copy plus its assembly definition. This avoids a committed symbol making a clean clone fail. Refresh again after updating the foundation source; validation detects an outdated copy.

Before uninstalling or replacing Firebase, turn off Firebase in TTGProjectConfig and click **Disable Firebase Adapter Before SDK Removal**. Wait for compilation. This removes only the reproducible local adapter, not SDK files, config assets or other integrations. Then follow vendor uninstall guidance, reviewing the selected Firebase-owned files and preserving the package-managed External Dependency Manager and unrelated plugins. After a supported reinstall, Refresh again.

Do not manually delete SDK DLLs while the generated adapter remains active. If this has already happened and the editor cannot compile, close Unity and remove only the generated `Assets/TTGGenerated/FirebaseAdapter` folder and its `.meta`, then reopen and repair the SDK. It is regenerated from source, not user-authored code.

## Git and automation

Commit template source, project settings and package manifest/lockfiles. Do not commit Assets/Firebase, Firebase native artifacts, generated adapters, installers or game configuration. Review `git status` before each commit: an ignore rule cannot untrack files already committed. A pre-existing repository requires a separately reviewed index-only untracking step; local SDK files need not be deleted.

CI must provision the same trusted asset artifacts without a duplicate dependency manager. Invoke public Editor method `TripleTapGames.Foundation.Editor.TTGFirebaseSetup.RefreshAdapter` in a Unity batch invocation; use a subsequent invocation for tests/builds after compilation has completed. No automatic downloader is included.

Firebase preparation does not clear Facebook/DOTween or other imported SDK redistribution rights, file-size checks, or release compliance. GitHub creation and upload are separate actions. Review the intended upload list before publishing.

## Verification completed on 25 September 2026

- Unity 2022.3.62f3: 29 EditMode tests passed in a clean Git-equivalent copy with Firebase/config files excluded.
- The same 29 tests passed with the existing SDK and generated adapter enabled.
- The same 29 tests passed after importing the local 13.13.0 Analytics and Crashlytics artifacts into the clean copy. For this automated check the vendor archives were filtered to omit bundled External Dependency Manager, matching the guided import selection; vendor asset contents were not modified.
- Repeated refresh preserved adapter timestamps and unrelated defines. Repeated disable removed only generated adapter code. Subsequent compilation, setup-window availability and re-enabling passed.
- Validation tests cover missing/partial SDKs, unreadable or conflicting version metadata, unsupported Firebase UPM imports, missing adapters and platform-specific config/native artifacts.
- The intended upload list excludes Firebase SDK/config/native paths. Its largest remaining file is approximately 3.6 MiB (a Facebook native DLL).
- The destination was not a Git repository, so no index changes or untracking were needed. No repository was published. SHA-256 inventory checks confirmed the destination's existing Assets/Firebase files were unchanged.

Native Android/iOS builds and live Firebase delivery were not exercised by these checks. Remaining imported SDKs still need redistribution review before publication.
