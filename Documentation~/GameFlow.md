# Connect your game's level flow

TTG Foundation does not define a level sequence, load gameplay content, or create Win/Lose UI. Every game keeps its own level architecture as the single source of truth. Games may optionally connect that architecture to the code-only `TTGLevelProgress` local store without converting or duplicating their level data.

The game owns:

- level ordering and stable level IDs;
- loading, unloading, Next, and Retry;
- when progression is recorded and how it affects navigation;
- win and loss detection and UI;
- protection against reporting an outcome more than once.

Foundation owns SDK initialization, consent, analytics fan-out, ads, IAP, validation, and build support.

See [Level Progress](LevelProgress.md) to add local unlocks and per-level best results without a Foundation level catalog or scene component.

## Required integration points

After initialization and analytics consent, report level start when interactive gameplay actually begins:

```csharp
TTGAnalytics.LevelStarted(levelId);
```

When the game accepts a successful outcome, report it once and notify the local interstitial rules:

```csharp
TTGAnalytics.LevelCompleted(levelId);
TTGAnalytics.MilestoneCompleted(levelNumber);
TTGAds.NotifyLevelCompleted(levelNumber);
```

When the game accepts a failed outcome, report it once:

```csharp
TTGAnalytics.LevelFailed(levelId);
```

`levelId` should be stable across releases. `levelNumber` is one-based. Analytics and ad gating are separate calls: `NotifyLevelCompleted` does not send an analytics event.

## Example game-owned bridge

Place a bridge like this under the consuming game's `Assets` folder and call it from the existing level controller:

```csharp
using TripleTapGames.Foundation;

public sealed class GameTTGEvents
{
    private string currentLevelId;
    private int currentLevelNumber;
    private bool outcomeReported;

    public void Started(string levelId, int levelNumber)
    {
        currentLevelId = levelId;
        currentLevelNumber = levelNumber;
        outcomeReported = false;
        TTGAnalytics.LevelStarted(currentLevelId);
    }

    public void Completed()
    {
        if (outcomeReported) return;
        outcomeReported = true;
        TTGAnalytics.LevelCompleted(currentLevelId);
        TTGAnalytics.MilestoneCompleted(currentLevelNumber);
        TTGAds.NotifyLevelCompleted(currentLevelNumber);
    }

    public void Failed()
    {
        if (outcomeReported) return;
        outcomeReported = true;
        TTGAnalytics.LevelFailed(currentLevelId);
    }
}
```

Games may add duration and other design events using the examples in [Analytics Events](AnalyticsEvents.md). Keep calls explicit so the game decides the exact lifecycle point and avoids duplicate vendor events.

## Migrating from Foundation 0.1.x

Version 0.2.0 removes `TTGLevelSequence`, `TTGLevelDefinition`, `TTGGameFlow`, and **Create / Update Base Game Flow**. Before upgrading a project that used them:

1. move level ordering and progress into the game's own system;
2. reconnect Next, Retry, Win, and Lose UI to that system;
3. remove scene objects with missing `TTGGameFlow` components and delete `DefaultLevelSequence.asset`;
4. add the explicit analytics and ad-gating calls shown above;
5. keep the generated Loading scene and `TTGBootstrap`—SDK initialization is unchanged.
