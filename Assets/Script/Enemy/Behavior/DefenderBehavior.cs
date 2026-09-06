using UnityEngine;

// Guards a fixed post. Will fight anything that comes near, but NEVER chases past
// its leash and never walks to the last known position.
//
// This is where "in case of defender, do not do that at all" is expressed —
// entirely inside this asset. EnemyController does not know defenders exist.
[CreateAssetMenu(menuName = "Enemy/Behavior/Defender")]
public class DefenderBehavior : ArchetypeBehavior
{
    [Header("Defender")]
    [SerializeField] private float defenseRadius = 3.5f;
    [SerializeField] private float strafeSwitchInterval = 2.5f;
    [SerializeField] private float anchorTolerance = 0.5f;

    // INPUT:  context with targetPos = player position, anchor = defend point
    // OUTPUT: movement + aim that keeps the enemy on its post
    // USE:    EnemyState.Engage
    //
    // Priority (highest first):
    //   1. Too far from post → return home immediately
    //   2. Player inside defend radius → strafe around player
    //   3. Player outside → hold the perimeter edge, never chase past the leash
    public override void Engage(EnemyContext c)
    {
        if (HandleAbilities(c)) return;   // abilities take priority over movement/firing
        c.aim.AimAt(c.targetPos);
        TickStrafeFlip(c, strafeSwitchInterval);

        float myDist     = Vector2.Distance(c.self.position, c.anchor);
        float playerDist = Vector2.Distance(c.targetPos,     c.anchor);

        if (myDist > defenseRadius)
            c.movement.MoveTowardSmart(c.anchor);
        else if (playerDist <= defenseRadius)
            c.movement.Strafe(c.targetPos, c.strafeClockwise);
        else
            c.movement.Strafe(c.anchor, c.strafeClockwise);

        SetFiring(c, true);
    }

    // INPUT:  context with targetPos = last known position
    // OUTPUT: walks home; aims at the last sighting while doing so
    // USE:    EnemyState.Investigate — defender never pursues, it just goes home
    public override void Investigate(EnemyContext c)
    {
        if (HandleAbilities(c)) return; // Use stale pos -> artillery
        c.aim.AimAt(c.targetPos);

        if (Vector2.Distance(c.self.position, c.anchor) > anchorTolerance)
            c.movement.MoveTowardSmart(c.anchor);
        else
            c.movement.Stop();

        SetFiring(c, false);
    }

    // INPUT:  context
    // OUTPUT: returns home then sweeps in place
    // USE:    EnemyState.Search
    public override void Search(EnemyContext c)
    {
        if (Vector2.Distance(c.self.position, c.anchor) > anchorTolerance)
            c.movement.MoveTowardSmart(c.anchor);
        else
            c.movement.Search();
    }

    // INPUT:  context
    // OUTPUT: stands still at the post, facing the defend point, scanning
    // USE:    EnemyState.Patrol (unaware) — defender does not wander, it holds position
    public override void Idle(EnemyContext c)
    {
        // Return to post if somehow displaced
        if (Vector2.Distance(c.self.position, c.anchor) > anchorTolerance)
            c.movement.MoveTowardSmart(c.anchor);
        else
        {
            c.movement.Stop();
            c.movement.FaceToward(c.anchor); // face the defend point to keep the cone centred on it
        }
    }

    // INPUT:  context
    // OUTPUT: none — exposed so EnemyController can draw the leash gizmo
    // USE:    Scene-view debugging only
    public float GetDefenseRadius() => defenseRadius;
}