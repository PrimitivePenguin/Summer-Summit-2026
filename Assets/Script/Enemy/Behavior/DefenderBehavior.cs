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
    [SerializeField] private float defenseRadius = 3.5f;         // leash length from anchor
    [SerializeField] private float strafeSwitchInterval = 2.5f;
    [SerializeField] private float wanderInterval = 3f;          // idle repositioning rate
    [SerializeField] private float anchorTolerance = 0.5f;       // "close enough to home"

    // INPUT:  context with targetPos = player position, anchor = spawn point
    // OUTPUT: leash-return, perimeter strafe, or measured advance — never past the leash
    // USE:    EnemyState.Engage
    public override void Engage(EnemyContext c)
    {
        c.aim.AimAt(c.targetPos);
        TickStrafeFlip(c, strafeSwitchInterval);

        float myDist = Vector2.Distance(c.self.position, c.anchor);
        float playerDist = Vector2.Distance(c.targetPos, c.anchor);

        if (myDist > defenseRadius)
            c.movement.MoveTowardSmart(c.anchor);                 // pulled off post — return
        else if (playerDist <= defenseRadius)
            c.movement.Strafe(c.targetPos, c.strafeClockwise);    // invader inside — circle them
        else if (myDist < defenseRadius * 0.8f)
            c.movement.MoveTowardSmart(c.targetPos);              // push toward the boundary
        else
            c.movement.Strafe(c.anchor, c.strafeClockwise);       // hold the border, stay mobile

        SetFiring(c, true);
    }

    // INPUT:  context with targetPos = last known position
    // OUTPUT: WATCHES the last sighting but walks back to the anchor
    // USE:    EnemyState.Investigate — the archetype-specific refusal to pursue
    public override void Investigate(EnemyContext c)
    {
        c.aim.AimAt(c.targetPos);

        if (Vector2.Distance(c.self.position, c.anchor) > anchorTolerance)
            c.movement.MoveTowardSmart(c.anchor);
        else
            c.movement.Stop();

        SetFiring(c, false);
    }

    // INPUT:  context
    // OUTPUT: finishes returning home, then sweeps in place
    // USE:    EnemyState.Search — overrides the default so the defender never commits
    //         to a distant last known position
    public override void Search(EnemyContext c)
    {
        if (Vector2.Distance(c.self.position, c.anchor) > anchorTolerance)
            c.movement.MoveTowardSmart(c.anchor);
        else
            c.movement.Search();
    }

    // INPUT:  context (anchor, wanderTarget, wanderTimer)
    // OUTPUT: picks random points inside the defense radius and drifts between them
    // USE:    EnemyState.Patrol — a defender has no route, it mills around its post
    public override void Idle(EnemyContext c)
    {
        c.wanderTimer -= Time.deltaTime;
        if (c.wanderTimer <= 0f || Vector2.Distance(c.self.position, c.wanderTarget) < 0.4f)
        {
            c.wanderTarget = c.anchor + (Random.insideUnitCircle * (defenseRadius * 0.7f));
            c.wanderTimer = wanderInterval;
        }

        c.movement.MoveTowardSmart(c.wanderTarget);
    }

    // INPUT:  context
    // OUTPUT: none — exposed so EnemyController can draw the leash gizmo
    // USE:    Scene-view debugging only
    public float GetDefenseRadius() => defenseRadius;
}