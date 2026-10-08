# Level progress

`TTGLevelProgress` is an optional local progress store for existing Unity games. It does not require a level configuration asset and does not load content, control scenes, create UI, or replace the game's current level controller.

The game supplies two values from its existing level source:

- `levelNumber`: the current one-based position used for linear unlocking;
- `levelId`: a stable, case-sensitive ID used to retain results if content is reordered.

Projects without an existing stable ID can start with IDs such as `level_001`. Do not derive an ID from a mutable display name.

## Connect an existing controller

Call `RecordAttempt` when interactive gameplay starts and `RecordCompletion` once after a genuine win is accepted:

```csharp
using TripleTapGames.Foundation;

public sealed class GameLevelProgressBridge
{
    public void LevelStarted(int zeroBasedIndex, string stableLevelId)
    {
        var levelNumber = zeroBasedIndex + 1;
        TTGLevelProgress.RecordAttempt(levelNumber, stableLevelId);
        TTGAnalytics.LevelStarted(stableLevelId);
    }

    public void LevelCompleted(int zeroBasedIndex, string stableLevelId, int stars, long score)
    {
        var levelNumber = zeroBasedIndex + 1;
        TTGLevelProgress.RecordCompletion(levelNumber, stableLevelId, stars, score);

        // Progress does not send these calls automatically.
        TTGAnalytics.LevelCompleted(stableLevelId);
        TTGAnalytics.MilestoneCompleted(levelNumber);
        TTGAds.NotifyLevelCompleted(levelNumber);
    }
}
```

The default track is named `default`. Pass a different `trackId` when a game needs independent progress for modes such as `campaign` and `daily`.

## Read progress

Use the numeric unlock state for navigation and the stable ID for per-level results:

```csharp
var snapshot = TTGLevelProgress.GetSnapshot();
var levelToOpen = snapshot.HighestUnlockedLevel;

var levelNumber = selectedIndex + 1;
var canPlay = TTGLevelProgress.IsUnlocked(levelNumber);
var result = TTGLevelProgress.GetLevel(selectedLevel.Id);
var completed = result != null && result.IsCompleted;
var bestStars = result?.BestStars ?? 0;
```

The game must clamp `HighestUnlockedLevel` to its own catalog. Completing the last known level intentionally records the next numeric unlock because Foundation does not own or know the catalog size.

Calling `RecordCompletion` for level N marks that stable ID complete and raises the track's highest unlock to at least N + 1. Out-of-order completion is accepted. Replays increment completion count while keeping the highest stars and score. `RecordAttempt` increments attempts separately.

## Migrate an existing local save

Read the legacy values in game-owned code and call `TrySeed` before recording new Foundation progress. Seeding succeeds only when the destination track has no Foundation save.

```csharp
using System.Collections.Generic;
using TripleTapGames.Foundation;
using UnityEngine;

public static class LegacyProgressMigration
{
    public static void MigrateOnce(IReadOnlyList<LegacyLevel> levels)
    {
        var highestUnlocked = Mathf.Max(1, PlayerPrefs.GetInt("Game.CurrentLevel", 0) + 1);
        var records = new List<TTGLevelProgressSeedRecord>();

        for (var index = 0; index < levels.Count; index++)
        {
            var level = levels[index];
            var stars = PlayerPrefs.GetInt("Game.Stars." + level.Id, 0);
            if (stars <= 0) continue;

            records.Add(new TTGLevelProgressSeedRecord(
                level.Id,
                index + 1,
                attemptCount: 0,
                completionCount: 1,
                bestStars: stars));
        }

        TTGLevelProgress.TrySeed(new TTGLevelProgressSeed(highestUnlocked, records));
    }
}
```

Replace `LegacyLevel`, keys, and index conversion with the existing project's actual save format. A completed seed record can raise the resulting unlock beyond the supplied `HighestUnlockedLevel` so the saved data remains internally consistent.

## Persistence rules

- Each named track is stored independently as versioned JSON in `PlayerPrefs` and saved immediately after a mutation.
- New or missing tracks begin with level 1 unlocked.
- Blank IDs, level numbers below 1, and negative stars, scores, or seed counts are rejected.
- Invalid saved JSON logs a warning and is treated as fresh progress. Its payload is not printed.
- `Reset(trackId)` deletes only the requested track.
- Progress APIs do not emit analytics, milestones, or ad-gating notifications.

PlayerPrefs is local device storage. Cloud synchronization, catalog limits, skips, branching progression, and conflict resolution remain game-owned.
