# Changelog

## 0.3.1 (unreleased)

- Resolve GameAnalytics remote interstitial rules in both readiness and show checks without mutating local assets. Existing keys and local fallback are preserved.
- Initialize Unity Gaming Services before Unity IAP, validate catalogs, expose receipt availability and restored purchase events, and reject duplicate in-flight product requests.
- Surface per-service startup failures and exception details, including optional failures; preserve required failures when another service is deferred.
- Reject active automatic Singular startup during builds and default new Singular configs to TTG-owned initialization.
- Add monetization regression tests and migration guidance.

- Marked all SDK adapter assemblies with `AlwaysLinkAssembly` so managed stripping discovers their runtime service registration, including the generated local Firebase adapter after refresh.

## 0.3.0

- Added optional code-only `TTGLevelProgress` persistence with named tracks, linear unlocks, stable per-level records, attempts, completions, stars, and scores.
- Added one-time legacy-save seeding without introducing a Foundation level catalog, gameplay controller, analytics side effects, or ad-gating side effects.
- Added integration and migration guidance for existing projects.

## 0.2.1

- Removed the Loading generator's final `Core` scene assumption.
- Preserved a valid configured game-scene destination or selected the first enabled non-Loading scene.

## 0.2.0

- Removed `TTGLevelSequence`, `TTGGameFlow`, the base-flow generator, and their tests from the Foundation runtime.
- Made level structure, progression persistence, Win/Lose UI, Next, and Retry entirely game-owned.
- Added gameplay-integration guidance for explicit level analytics, milestone, and ad-gating calls.
- This is a breaking release for projects that used the removed prefab game-flow APIs.

## 0.1.5

- Added a complete manual analytics-events guide with copy-ready game-owned examples.
- Clarified that base game-flow outcomes do not emit analytics automatically.

## 0.1.4

- Removed automatic analytics emission from initialization, game flow, Unity IAP, and AppLovin callbacks.
- Kept the analytics facade and vendor fan-out available for explicit calls from each game's `Assets` scripts.

## 0.1.3

- Added Singular analytics fan-out and native AppLovin ad-revenue reporting.
- Added automatic UTC-day retention events.
- Added one-time level 5, 10, 15, and 20 completion milestones to the base game flow.

## 0.1.2

- Changed the guided Facebook SDK pin to version 17.0.0.

## 0.1.1

- Added a complete package installation and usage guide.
- Linked the setup window and package manifest directly to the documentation.

## 0.1.0

- Initial TTG initialization, privacy, analytics, ads, IAP, setup, validation, and build-processing foundation.
