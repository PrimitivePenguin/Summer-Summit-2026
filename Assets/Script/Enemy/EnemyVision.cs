using UnityEngine;

// Detection only. Purely an information source — issues no commands and knows
// nothing about states. FieldOfView.cs draws the cone; this decides what is seen.
//
// Three concentric rings:
//   loadDistance  — outermost gate. Beyond it the enemy can go fully idle.
//   viewDistance  — the real cone: direction + line of sight required.
//   senseDistance — omnidirectional "something is right behind me", LOS still required.
//
// OUTPUT: canSeePlayer, awareness, lastKnownPosition, hasLastKnown
public class EnemyVision : MonoBehaviour
{
    [Header("Cone + LOS")]
    [SerializeField] LayerMask wallLayer;                 // layers that block sight
    [SerializeField] public float viewDistance = 8f;      // cone reach — FieldOfView reads this
    [SerializeField] public float fovAngle = 90f;         // cone width — FieldOfView reads this

    [Header("Proximity Ranges")]
    [SerializeField] float senseDistance = 4f;            // omnidirectional, no cone
    [SerializeField] float loadDistance = 15f;            // outermost idle gate

    [Header("Awareness")]
    [SerializeField] float awarenessDecayRate = 0.5f;     // lost per second once contact breaks

    [Header("Debug")]
    [SerializeField] bool drawLastKnown = true;
    [SerializeField] bool drawRanges = false;

    // ── Read-only state; EnemyController reads these ──────────────────────────

    public bool canSeePlayer { get; private set; }          // cone + LOS satisfied this frame
    public float awareness { get; private set; }            // 1 = fresh contact, decays to 0
    public Vector2 lastKnownPosition { get; private set; }  // most recent fix on the player
    public bool hasLastKnown { get; private set; }          // false until the FIRST sighting

    Transform player;
    AimController aimController;

    // INPUT:  player transform, aimController
    // OUTPUT: stores both; auto-fills wallLayer if left at zero
    // USE:    called once by EnemyController.Start
    public void Initialize(Transform player, AimController aimController)
    {
        this.player = player;
        this.aimController = aimController;

        if (wallLayer == 0)
            wallLayer = LayerMask.GetMask("Collision");
    }

    // INPUT:  player position, awarenessDecayRate
    // OUTPUT: updates canSeePlayer, awareness, lastKnownPosition, hasLastKnown
    // USE:    called every frame by EnemyController before any transition logic
    // NOTE:   hasLastKnown exists because (0,0) is a perfectly valid world position.
    //         Without the flag, an enemy that had never seen the player would happily
    //         path to world origin.
    public void Tick()
    {
        if (player == null) return;

        canSeePlayer = CheckPlayerVisible();
        Debug.DrawLine(transform.position + Vector3.left * 0.4f, transform.position + Vector3.right * 0.4f, Color.black);
        Debug.DrawLine(transform.position + Vector3.down * 0.4f, transform.position + Vector3.up * 0.4f, Color.black);


        if (canSeePlayer || IsPlayerInSenseRange())
        {
            awareness = 1f;
            lastKnownPosition = player.position;
            hasLastKnown = true;
        }
        else
        {
            awareness = Mathf.Clamp01(awareness - awarenessDecayRate * Time.deltaTime);
        }
    }

    // INPUT:  none
    // OUTPUT: clears awareness, the remembered fix, and its validity flag
    // USE:    called by EnemyController on entering Patrol — the enemy has finished
    //         searching and genuinely given up
    public void ForgetLastKnown()
    {
        awareness = 0f;
        hasLastKnown = false;
    }

    // INPUT:  player position, viewDistance, fovAngle, wallLayer
    // OUTPUT: true only if inside the cone AND unobstructed
    // USE:    distance reject → angle reject → linecast, cheapest test first
    bool CheckPlayerVisible()
    {
        if (player == null) return false;

        Vector2 self = transform.position;
        Vector2 target = player.position;
        Vector2 toPlayer = target - self;

        // 1. Distance
        if (toPlayer.sqrMagnitude > viewDistance * viewDistance) return false;

        // 2. Cone. Uses transform.up directly rather than an angle from AimController,
        //    which makes it immune to degree-offset mismatches between the two.
        //    Switch to transform.right if your sprite faces RIGHT at zero rotation.
        if (Vector2.Angle(transform.up, toPlayer) > fovAngle * 0.5f) return false;

        // 3. Walls
        RaycastHit2D hit = Physics2D.Linecast(self, target, wallLayer);
        return hit.collider == null;
    }

    // INPUT:  player position, senseDistance, wallLayer
    // OUTPUT: true if the player is close and unobstructed, regardless of facing
    // USE:    stops the player sneaking up directly behind an enemy in an open room
    // NOTE:   if the enemy's own collider sits on wallLayer, set
    //         Physics2D.queriesStartInColliders = false or this linecast self-blocks
    //         every frame and the enemy goes permanently blind
    public bool IsPlayerInSenseRange()
    {
        if (player == null) return false;

        Vector2 self = transform.position;
        Vector2 target = player.position;

        if ((target - self).sqrMagnitude > senseDistance * senseDistance) return false;

        RaycastHit2D hit = Physics2D.Linecast(self, target, wallLayer);
        return hit.collider == null;
    }

    // INPUT:  player position, loadDistance
    // OUTPUT: true if the player is close enough for this enemy to be worth simulating
    // USE:    EnemyController's Idle gate — distant enemies skip pathfinding entirely
    public bool IsPlayerInLoadRange()
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.position) < loadDistance;
    }

    // INPUT:  none
    // OUTPUT: none — Scene-view only
    // USE:    the sphere is the remembered player position, red when fresh and fading
    //         to grey as awareness decays. If the line points somewhere the player
    //         never stood, awareness or lastKnownPosition tracking is wrong.
    private void OnDrawGizmos()
    {
        if (drawRanges)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, senseDistance);
            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, loadDistance);
        }

        if (!drawLastKnown || !Application.isPlaying) return;
        if (!hasLastKnown || awareness <= 0f) return;

        Color c = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.4f), Color.red, awareness);
        Gizmos.color = c;
        Gizmos.DrawWireSphere(lastKnownPosition, 0.35f + 0.25f * awareness);
        Gizmos.DrawLine(transform.position, lastKnownPosition);
    }
}