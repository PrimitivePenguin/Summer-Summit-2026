using UnityEngine;

// Keeps the player in a preferred distance band: backs off when crowded,
// closes when the player drifts away, circle-strafes in the sweet spot.
[CreateAssetMenu(menuName = "Enemy/Behavior/Skirmisher")]
public class SkirmisherBehavior : ArchetypeBehavior
{
    [Header("Skirmisher")]
    [SerializeField] private float preferredMinDist = 4.0f;      // retreat inside this
    [SerializeField] private float preferredMaxDist = 7.0f;      // advance outside this
    [SerializeField] private float strafeSwitchInterval = 2.5f;  // seconds between flips

    // INPUT:  context with targetPos = player position
    // OUTPUT: retreat / advance / strafe depending on the distance band; fires when aimed
    // USE:    EnemyState.Engage
    public override void Engage(EnemyContext c)
    {
        if (HandleAbilities(c)) return;   // abilities take priority over movement/firing
        c.aim.AimAt(c.targetPos);
        TickStrafeFlip(c, strafeSwitchInterval);

        if (c.distToTarget < preferredMinDist)
            c.movement.MoveAwayFrom(c.targetPos);            // too close — back up
        else if (c.distToTarget > preferredMaxDist)
            c.movement.MoveTowardSmart(c.targetPos);         // too far — close the gap
        else
            c.movement.Strafe(c.targetPos, c.strafeClockwise); // sweet spot — orbit

        SetFiring(c, true);   // SetFiring still gates on actually facing the player
    }

    // INPUT:  context with targetPos = last known position
    // OUTPUT: advances on the last sighting, holding fire
    // USE:    EnemyState.Investigate
    public override void Investigate(EnemyContext c)
    {
        c.aim.AimAt(c.targetPos);
        c.movement.MoveTowardSmart(c.targetPos);
        SetFiring(c, false);
    }

    // INPUT:  context
    // OUTPUT: follows the assigned PatrolRoute
    // USE:    EnemyState.Patrol
    public override void Idle(EnemyContext c) => c.movement.Patrol();
}