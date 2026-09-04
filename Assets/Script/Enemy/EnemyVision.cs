using UnityEngine;
// loadDistance: loads player into memory
// senseDistance: knows player is nearby
// viewDist: can see player -> check with cone + LOS
// Output: canSeePlayer, awareness, lastKnownPosition

public class EnemyVision : MonoBehaviour
{  
    // Configuration
    [Header("Cone + LOS")]
    [SerializeField] LayerMask wallLayer;         // layers that block sight (same layer FOV uses)
    [SerializeField] float viewDistance = 8f;     // cone reach — match FieldOfView.viewDistance
    [SerializeField] float fovAngle = 90f;        // cone width — match FieldOfView.fovAngle

    [Header("Proximity Ranges")]
    [SerializeField] float senseDistance = 12f;   // omnidirectional last-known range (no cone, no LOS)
    [SerializeField] float loadDistance = 15f;    // outermost gate: beyond this, go fully idle

    [Header("Awareness")]
    [SerializeField] float awarenessDecayRate = 0.5f; // awareness lost per second once contact breaks

    // ---- read-only state; EnemyAI / EnemyController reads these ----
    public bool canSeePlayer { get; private set; }         // true ONLY inside cone + LOS this frame
    public float awareness { get; private set; }           // 1 = fresh sighting, decays to 0
    public Vector2 lastKnownPosition { get; private set; } // most recent fix on the player

    Transform player;
    AimController aimController;

    // Initialize: called from the enemyController's Start()
    public void Initialize(Transform player, AimController aimController)
    {
        this.player = player;
        this.aimController = aimController;
    }

    // called every frame by the controller (this component never self-Updates)
    public void Tick()
    {
        // Returns 3 variables every tick
        canSeePlayer = CheckPlayerVisible();    

        if (canSeePlayer)
        {
            awareness = 1f;
            lastKnownPosition = player.position;       
        }
        else if (IsPlayerInSenseRange())             
        {
            lastKnownPosition = player.position;
        }
        else
        {
            // no contact of either kind — bleed awareness off over time
            awareness = Mathf.Clamp01(awareness - awarenessDecayRate * Time.deltaTime);
        }
    }

    // Player inside the cone AND within viewDistance AND no wall in between
    bool CheckPlayerVisible()
    {
        if (player == null || aimController == null) return false;

        Vector2 self = transform.position;
        Vector2 toPlayer = (Vector2)player.position - self;
        float dist = toPlayer.magnitude;
        if (dist > viewDistance) return false;                        // too far

        // undo AimController's -90 offset to get a standard math-degree facing
        float facingAngle = aimController.GetFacingAngle() + 90f;
        float angleToPlayer = Mathf.Atan2(toPlayer.y, toPlayer.x) * Mathf.Rad2Deg;
        if (Mathf.Abs(Mathf.DeltaAngle(facingAngle, angleToPlayer)) > fovAngle / 2f)
            return false;                                             // outside the cone

        // wall between us? (normalized dir + dist gives a clean, length-limited ray)
        RaycastHit2D hit = Physics2D.Raycast(self, toPlayer.normalized, dist, wallLayer);
        return hit.collider == null;                                  // clear line of sight
    }

    public bool IsPlayerInSenseRange()
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.position) < senseDistance;
    }

    // Load player if in range, otherwise unload = fully idle, no awareness, no lastKnownPosition
    public bool IsPlayerInLoadRange()
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.position) < loadDistance;
    }
}