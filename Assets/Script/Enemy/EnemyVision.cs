using UnityEngine;

// Detection only. Issues no commands, knows nothing about states.
//
//   loadDistance  — outermost gate; beyond it the enemy idles.
//   viewDistance  — the real cone: direction + LOS.
//   senseDistance — omnidirectional proximity, LOS still required.
//

public class EnemyVision : MonoBehaviour
{
    [Header("Cone + LOS")]
    [SerializeField] LayerMask wallLayer;
    [SerializeField] public float viewDistance = 8f;   // FieldOfView reads this
    [SerializeField] public float fovAngle = 90f;      // FieldOfView reads this

    [Header("Proximity Ranges")]
    [SerializeField] float senseDistance = 4f;
    [SerializeField] float loadDistance = 15f;

    [Header("Awareness")]
    [SerializeField] float awarenessDecayRate = 0.5f;

    [Header("Debug")]
    [SerializeField] bool drawLastKnown = true;
    [SerializeField] bool drawRanges = false;
    [SerializeField] bool drawFacingCross = false;

    // ── Read-only state ───────────────────────────────────────────────────────
    public bool canSeePlayer { get; private set; }          // cone + LOS this frame
    public bool sensedPlayer { get; private set; }          // proximity + LOS this frame
    public bool hasContact => canSeePlayer || sensedPlayer; // any live fix on the player
    public float awareness { get; private set; }
    public Vector2 lastKnownPosition { get; private set; }
    public bool hasLastKnown { get; private set; }

    Transform player;
    AimController aimController;


    public void AlertToPosition(Vector2 soundOrigin, float alertAwareness = 1.0f)
    {
        lastKnownPosition = soundOrigin;
        hasLastKnown = true;
        awareness = Mathf.Max(awareness, alertAwareness);
    }

    // INPUT:  player transform, aim controller
    // OUTPUT: stores refs, defaults wallLayer
    // USE:    once from EnemyController.Start
    public void Initialize(Transform player, AimController aimController)
    {
        this.player = player;
        this.aimController = aimController;
        if (wallLayer == 0) wallLayer = LayerMask.GetMask("Collision");
    }

    // INPUT:  player position, decay rate
    // OUTPUT: canSeePlayer, sensedPlayer, awareness, lastKnownPosition, hasLastKnown
    // USE:    every frame, before transition logic. REPLACES old Tick
    public void Tick()
    {
        if (player == null) return;

        if (PlayerDive.IsPlayerSubmerged)
        {
            canSeePlayer = false;
            awareness = Mathf.Max(0f, awareness - Time.deltaTime * 2f);
            return;
        }

        canSeePlayer = CheckPlayerVisible();
        sensedPlayer = CheckPlayerSensed();

        if (drawFacingCross)
        {
            Debug.DrawLine(transform.position + Vector3.left * 0.4f, transform.position + Vector3.right * 0.4f, Color.black);
            Debug.DrawLine(transform.position + Vector3.down * 0.4f, transform.position + Vector3.up * 0.4f, Color.black);
        }

        if (hasContact)
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
    // OUTPUT: awareness 0, hasLastKnown false
    // USE:    on entering Patrol — the enemy has given up
    public void ForgetLastKnown()
    {
        awareness = 0f;
        hasLastKnown = false;
    }

    // INPUT:  player position, viewDistance, fovAngle, wallLayer
    // OUTPUT: true if in cone AND unobstructed
    // USE:    Tick. Distance → angle → linecast, cheapest first
    bool CheckPlayerVisible()
    {
        Vector2 self = transform.position;
        Vector2 toPlayer = (Vector2)player.position - self;

        if (toPlayer.sqrMagnitude > viewDistance * viewDistance) return false;
        if (Vector2.Angle(transform.up, toPlayer) > fovAngle * 0.5f) return false;

        return Physics2D.Linecast(self, player.position, wallLayer).collider == null;
    }

    // INPUT:  player position, senseDistance, wallLayer
    // OUTPUT: true if close AND unobstructed, any direction
    // USE:    Tick
    bool CheckPlayerSensed()
    {
        Vector2 self = transform.position;
        Vector2 toPlayer = (Vector2)player.position - self;

        if (toPlayer.sqrMagnitude > senseDistance * senseDistance) return false;
        return Physics2D.Linecast(self, player.position, wallLayer).collider == null;
    }

    // INPUT:  none
    // OUTPUT: cached sense result from this frame's Tick
    // USE:    external callers (kept for API compatibility)
    public bool IsPlayerInSenseRange() => sensedPlayer;

    // INPUT:  player position, loadDistance
    // OUTPUT: true if worth simulating
    // USE:    EnemyController Idle gate
    public bool IsPlayerInLoadRange()
        => player != null && Vector2.Distance(transform.position, player.position) < loadDistance;

    // INPUT:  none
    // OUTPUT: Scene view only
    // USE:    remembered fix (red → grey as awareness decays), optional range rings
    private void OnDrawGizmos()
    {
        if (drawRanges)
        {
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, senseDistance);
            Gizmos.color = new Color(0.4f, 0.4f, 0.4f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, loadDistance);
        }

        if (!drawLastKnown || !Application.isPlaying || !hasLastKnown || awareness <= 0f) return;

        Gizmos.color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.4f), Color.red, awareness);
        Gizmos.DrawWireSphere(lastKnownPosition, 0.35f + 0.25f * awareness);
        Gizmos.DrawLine(transform.position, lastKnownPosition);
    }
}