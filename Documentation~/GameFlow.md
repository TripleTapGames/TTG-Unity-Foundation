# Base game flow

Run **Tools > Triple Tap Games > Create or Update Base Game Flow** after saving Core. The tool preserves existing Core roots and creates:

- `Assets/Game/Config/DefaultLevelSequence.asset`
- one `TTGGameFlow` controller with a `LevelRoot`
- `TTGGameFlowUI` with a level label, Win panel/Next button, and Lose panel/Retry button
- one EventSystem so pointer and touch clicks reach the generated buttons
- Loading and Core as the first two enabled Build Settings scenes

Create each playable level as a prefab, select DefaultLevelSequence, and add the prefabs in the order they should be played. Give every entry a stable Level ID such as `Level_001`; that ID is used for analytics. The default entry deliberately has no prefab, so the template remains gameplay-agnostic.

When gameplay reaches an outcome, call exactly one of:

```csharp
TTGGameFlow.Instance.WinLevel();
TTGGameFlow.Instance.LoseLevel();
```

Win and Lose are accepted only while the current state is Playing, so duplicate collision callbacks do not duplicate analytics or panels. Next persists the next sequence index and loads its prefab. Retry reloads the same entry. For test play, the TTGGameFlow component context menu includes Debug Win and Debug Lose.

The flow sends `TTGAnalytics.LevelStarted`, `LevelCompleted`, and `LevelFailed`. Every initialized TTG analytics provider receives the normalized event. The GameAnalytics adapter translates these three event names into native `GAProgressionStatus.Start`, `Complete`, and `Fail` progression events; other TTG events remain GameAnalytics design events. Level completion also calls `TTGAds.NotifyLevelCompleted` for interstitial rules.

The controller belongs in Core and does not use DontDestroyOnLoad. Core remains loaded while its assigned level prefabs are replaced under LevelRoot. Loading UI and SDK bootstrap objects are separate concerns.
