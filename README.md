# TTG Foundation

TTG Foundation is the shared Unity integration layer for Triple Tap Games. It targets Unity 2022.3 LTS and newer.

## Install from Git

Add both dependencies to the `dependencies` object in the consuming project's `Packages/manifest.json`:

```json
"com.cysharp.unitask": "https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11",
"com.tripletapgames.foundation": "https://github.com/TripleTapGames/TTG-Unity-Foundation.git#v0.1.3"
```

Use an immutable release tag in production projects. UniTask is a required companion dependency; optional vendor SDKs are installed only when that integration is needed.

Open **Tools > Triple Tap Games > Project Setup**, install or import the dependencies required by the game, create local configuration assets, and run validation before building.

Firebase Analytics and Crashlytics 13.13.0 are imported separately using the official Unity artifacts. Do not add Firebase binaries or project configuration files to this repository.

Facebook is a guided local import pinned to SDK 17.0.0.

Populated configuration belongs to the consuming game, not this package. Runtime SDK keys are shipped in application binaries and must not be confused with server-side secrets. Never store privileged backend credentials in a Unity client.

Start with the [complete user guide](Documentation~/UserGuide.md). Shorter references are available in [Getting Started](Documentation~/GettingStarted.md), [Firebase Installation](Documentation~/FirebaseInstallation.md), and [Upgrading SDKs](Documentation~/Upgrading.md).
