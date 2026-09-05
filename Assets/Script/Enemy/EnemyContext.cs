using UnityEngine;

// Everything an ArchetypeBehavior needs to act, plus per-enemy scratch state.
//
// WHY THIS EXISTS: ArchetypeBehavior is a ScriptableObject, and a ScriptableObject
// asset is SHARED by every enemy that references it. Mutable per-enemy state
// (strafe timers, wander targets) must never live on the asset or all skirmishers
// using that asset would strafe in lockstep off one timer.
//
// One EnemyContext instance is created per enemy in EnemyController.Start.
// Plain C# class — not a MonoBehaviour, not a ScriptableObject.
public class EnemyContext
{
    // ── Wiring: set once in EnemyController.Start ─────────────────────────────
    public Transform self;
    public EnemyMovement movement;
    public AimController aim;
    public BulletSpawn bulletSpawn;
    public EnemyVision vision;
    public Vector2 anchor;          // spawn position — the Defender's home post

    // ── Per-frame slice: refreshed by EnemyController.Update ──────────────────
    // targetPos is the player position while Engaging, and vision.lastKnownPosition
    // in every other state. Behaviors never decide which — they just read it.
    public Vector2 targetPos;
    public float distToTarget;

    // ── Per-enemy scratch: behaviors read and write these freely ──────────────
    public bool strafeClockwise = true;
    public float strafeTimer;
    public Vector2 wanderTarget;
    public float wanderTimer;
}