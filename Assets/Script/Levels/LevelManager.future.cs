using UnityEngine;

// ─────────────────────────────────────────────────────────────────────────────
// FUTURE LEVEL-MANAGER IMPROVEMENTS
//
// This file does NOT use partial classes. Add new public methods here and call
// them from your own systems. The two private hooks (OnLevelStarted / OnLevelEnded)
// live as stubs in LevelManager.cs — replace their bodies there when you are ready.
//
// Candidate features (enemy_ai_architecture.md §13 + LevelController.cs):
//   - Squad awareness sharing   → LevelManager.ReportPlayerSighting
//   - Level unlock persistence  → LevelManager.UnlockNextLevel
//   - Difficulty scaling        → LevelManager.GetDifficultyTier
//   - Spawn waves / reinforcements
//   - Per-level EnemyData overrides
// ─────────────────────────────────────────────────────────────────────────────
public class LevelManagerExtensions
{
    // Standalone helper — call LevelManager.Instance.ReportPlayerSighting(...)
    // once you implement squad awareness sharing.

    // INPUT:  reporter enemy, world position where the player was seen
    // OUTPUT: none (stub)
    // USE:    an enemy that spots the player calls this so the LevelManager can
    //         broadcast lastKnownPosition to allies within range (Medium difficulty)
    public static void ReportPlayerSighting(EnemyController reporter, Vector2 position)
    {
        // TODO: iterate over registered enemies, update their vision.lastKnownPosition
    }

    // INPUT:  none
    // OUTPUT: none (stub)
    // USE:    call after a level ends to unlock the next entry in LevelController
    public static void UnlockNextLevel()
    {
        // TODO: PlayerPrefs "UnlockedLevel" / "ReachedIndex" logic (see LevelController.cs)
    }
}