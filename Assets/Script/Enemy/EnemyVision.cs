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
    [SerializeField] public float viewDistance = 8f;     // cone reach — match FieldOfView.viewDistance
    [SerializeField] public float fovAngle = 90f;        // cone width — match FieldOfView.fovAngle

    [Header("Proximity Ranges")]
    [SerializeField] float senseDistance = 4f;   // omnidirectional last-known range (no cone, no LOS)
    [SerializeField] float loadDistance = 15f;    // outermost gate: beyond this, go fully idle

    [Header("Awareness")]
    [SerializeField] float awarenessDecayRate = 0.5f; // awareness lost per second once contact breaks

    // ---- read-only state; EnemyAI / EnemyController reads these ----
    public bool canSeePlayer { get; private set; }         // true ONLY inside cone + LOS this frame
    public float awareness { get; private set; }           // 1 = fresh sighting, decays to 0
    public Vector2 lastKnownPosition { get; private set; } // most recent fix on the player

    Transform player;
    AimController aimController;


    // INPUT: player transform, aimController
    // OUTPUT: stores both, auto-fills walllayer to collision
    public void Initialize(Transform player, AimController aimController)
    {
        this.player = player;
        this.aimController = aimController;

        if (wallLayer == 0){
            wallLayer = LayerMask.GetMask("Collision");
        }
    }

    // INPUT: player position, aimController facing angle, awarenessDecayRate
    // OUTPUT: updates canSeePlayer, awareness, and lastKnownPosition
    // called every frame by the controller
    public void Tick()
    {
        if (player == null) return;

        // Returns 3 variables every tick
        canSeePlayer = CheckPlayerVisible();    

        if (canSeePlayer)
        {
            // Debug.Log("I see you!");
            awareness = 1f;
            lastKnownPosition = player.position;       
        }
        else if (IsPlayerInSenseRange())             
        {
            awareness = 1f;
            lastKnownPosition = player.position;
        }
        else
        {
            // no contact of either kind — bleed awareness off over time
            awareness = Mathf.Clamp01(awareness - awarenessDecayRate * Time.deltaTime);
        }
    }

    // INPUT: player position, viewDistance, fovAngle, wallLayer
    // OUTPUT: distance -> angle -> raycast -> true
    bool CheckPlayerVisible()
    {
        if (player == null) return false;

        Vector2 self = transform.position;
        Vector2 target = player.position;
        Vector2 toPlayer = target - self;

        // 1. Distance check
        if (toPlayer.sqrMagnitude > viewDistance * viewDistance) return false;

        // 2. Physical transform forward check (immune to angle/offset mismatches)
        // Use transform.up if your art faces UP at 0 rot, or transform.right if it faces RIGHT
        Vector2 facingDir = transform.up; 
        float angleToPlayer = Vector2.Angle(facingDir, toPlayer);

        if (angleToPlayer > fovAngle * 0.5f)
            return false; // Outside cone

        // 3. Linecast for walls
        RaycastHit2D hit = Physics2D.Linecast(self, target, wallLayer);
        return hit.collider == null;
    }

    public bool IsPlayerInSenseRange()
    {
        if (player == null) return false;

        Vector2 self = transform.position;
        Vector2 target = player.position;

        // Proximity distance check
        if ((target - self).sqrMagnitude > senseDistance * senseDistance)
            return false;

        // Blocked by walls
        RaycastHit2D hit = Physics2D.Linecast(self, target, wallLayer);
        return hit.collider == null;
    }

    // Load player if in range, otherwise unload = fully idle, no awareness, no lastKnownPosition
    public bool IsPlayerInLoadRange()
    {
        if (player == null) return false;
        return Vector2.Distance(transform.position, player.position) < loadDistance;
    }
}