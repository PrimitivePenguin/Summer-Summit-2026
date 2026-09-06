using UnityEngine;

// Sprints into point-blank range, stops, and blasts.
// Investigates aggressively; patrols a route when unaware.
[CreateAssetMenu(menuName = "Enemy/Behavior/Chaser")]
public class ChaserBehavior : ArchetypeBehavior
{
    [Header("Chaser")]
    [SerializeField] private float attackRange = 2.2f;   // stop-and-shoot distance

    // INPUT:  context with targetPos = player position
    // OUTPUT: closes to attackRange via A*, then holds still and fires
    // USE:    EnemyState.Engage
    public override void Engage(EnemyContext c)
    {
        if (HandleAbilities(c)) return;   // abilities take priority over movement/firing
        c.aim.AimAt(c.targetPos);

        if (c.distToTarget <= attackRange)
            c.movement.Stop();                       // hold position while firing
        else
            c.movement.MoveTowardSmart(c.targetPos); // pathed, not straight-line

        SetFiring(c, c.distToTarget <= attackRange);
    }

    // INPUT:  context with targetPos = last known position
    // OUTPUT: walks to where the player was last seen, holding fire
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