using UnityEngine;

// One asset per enemy personality. Subclass this to add a new archetype —
// EnemyController never needs a new switch case, and no existing file changes.
//
// EnemyController decides WHICH state the enemy is in (from vision alone).
// ArchetypeBehavior decides HOW this archetype executes that state.
//
// INPUT:  EnemyContext (wiring + per-frame target + per-enemy scratch)
// OUTPUT: movement commands, aim commands, firing toggle
//
// REMINDER: this is a shared asset. Store nothing mutable in fields here —
// put per-enemy state on EnemyContext.
public abstract class ArchetypeBehavior : ScriptableObject
{
    [Header("Firing")]
    [SerializeField] protected float aimTolerance = 15f;   // degrees of error allowed before firing

    // INPUT:  context with targetPos = live player position
    // OUTPUT: movement + aim + firing for "I can see the player right now"
    // USE:    called every frame while EnemyState.Engage
    public abstract void Engage(EnemyContext c);

    // INPUT:  context with targetPos = vision.lastKnownPosition
    // OUTPUT: movement + aim for "I lost sight but still remember where you were"
    // USE:    called every frame while EnemyState.Investigate
    public abstract void Investigate(EnemyContext c);

    // INPUT:  context (targetPos is stale and should generally be ignored)
    // OUTPUT: idle behavior — patrol, guard-wander, whatever suits the archetype
    // USE:    called every frame while EnemyState.Patrol (awareness has hit zero)
    public abstract void Idle(EnemyContext c);

    // INPUT:  context
    // OUTPUT: sweep behavior for "I reached the last known position and found nothing"
    // USE:    called every frame while EnemyState.Search
    // NOTE:   default is stand and sweep. Defender overrides it to hold its anchor.
    //         EnemyController calls movement.StartSearching() on state ENTRY, so this
    //         method must never call it — doing so would reset the sweep every frame.
    public virtual void Search(EnemyContext c) => c.movement.Search();

    // INPUT:  context, whether this archetype wants to shoot right now
    // OUTPUT: sets bulletSpawn.isAutomaticSpawn, gated on actually facing the target
    // USE:    every firing decision routes through here, so no archetype can fire
    //         while pointed at a wall
    protected void SetFiring(EnemyContext c, bool wantsToFire)
    {
        if (c.bulletSpawn == null) return;
        Debug.Log($"[{c.self.name}] wants to fire but not aimed (tol {aimTolerance}°)");

        c.bulletSpawn.isAutomaticSpawn =
            wantsToFire && c.aim != null && c.aim.IsAimedAt(c.targetPos, aimTolerance);
    }

    // INPUT:  context, seconds between direction flips
    // OUTPUT: counts down c.strafeTimer and flips c.strafeClockwise on expiry
    // USE:    stops a strafing enemy grinding against the same obstacle forever
    // NOTE:   reads and writes the CONTEXT, never a field on this asset
    protected void TickStrafeFlip(EnemyContext c, float interval)
    {
        c.strafeTimer -= Time.deltaTime;
        if (c.strafeTimer <= 0f)
        {
            c.strafeClockwise = !c.strafeClockwise;
            c.strafeTimer = interval;
        }
    }
}