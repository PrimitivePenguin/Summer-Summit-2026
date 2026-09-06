using UnityEngine;

// Rotates the root transform to face a target and pushes the resulting arc into
// BulletSpawn. Attach to Player root and Enemy root.


public class AimController : MonoBehaviour
{
    [Header("Turn")]
    public bool isTurnRate = true;
    public float turnRate = 180f;   // degrees per second

    private float currentAngle;
    private Rigidbody2D rb;
    private BulletSpawn bulletSpawn;

    // INPUT:  none
    // OUTPUT: current facing angle (degrees, "up = 0" convention)
    // USE:    EnemyMovement.StartSearching, IsAimedAt
    public float GetFacingAngle() => currentAngle;

    // INPUT:  none
    // OUTPUT: caches refs, reads initial angle, applies turnRate mode, normalises rotation
    // USE:    REPLACES old Awake (adds rb cache + SnapTo)
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bulletSpawn = GetComponentInChildren<BulletSpawn>();

        currentAngle = transform.eulerAngles.z;
        if (!isTurnRate) turnRate = Mathf.Infinity;

        SnapTo(currentAngle);   // strips any stray X/Y rotation baked into the prefab
    }

    // INPUT:  world position
    // OUTPUT: angle (degrees) to face it, with the -90 Vector2.up offset Bullet.cs expects
    // USE:    AimAt, IsAimedAt, Player firing arc
    public float GetAngleTo(Vector2 targetPos)
    {
        Vector2 direction = targetPos - (Vector2)transform.position;
        return Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
    }

    // INPUT:  world position
    // OUTPUT: rotates toward it (turn-rate limited), updates firing arc
    // USE:    every frame by behaviors/Player. REPLACES old AimAt (no duplicated body)
    public void AimAt(Vector2 targetPos) => AimAtAngle(GetAngleTo(targetPos));

    // INPUT:  target angle in degrees
    // OUTPUT: rotates toward it (turn-rate limited), updates firing arc
    // USE:    Search sweep, AimAt
    public void AimAtAngle(float targetAngle)
    {
        if (!isTurnRate || float.IsInfinity(turnRate))
            currentAngle = targetAngle;
        else if (Time.deltaTime > 0f)
            currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnRate * Time.deltaTime);

        if (float.IsNaN(currentAngle)) currentAngle = targetAngle;
        ApplyRotation();
    }

    // INPUT:  angle in degrees
    // OUTPUT: instantly sets facing
    // USE:    spawn, teleport, Awake normalisation
    public void SnapTo(float angle)
    {
        currentAngle = angle;
        ApplyRotation();
    }

    // INPUT:  target position, tolerance in degrees
    // OUTPUT: true if facing within tolerance
    // USE:    ArchetypeBehavior.SetFiring gate
    public bool IsAimedAt(Vector2 targetPos, float tolerance = 5f)
        => Mathf.Abs(Mathf.DeltaAngle(currentAngle, GetAngleTo(targetPos))) < tolerance;

    // INPUT:  currentAngle
    // OUTPUT: writes transform + rigidbody rotation, pushes spread arc to BulletSpawn
    // USE:    single funnel for every rotation write
    private void ApplyRotation()
    {
        transform.rotation = Quaternion.Euler(0f, 0f, currentAngle);
        if (rb != null) rb.rotation = currentAngle;

        if (bulletSpawn != null)
        {
            float halfAngle = bulletSpawn.GetCurrentData().spreadAngle * 0.5f;
            bulletSpawn.SetFiringArc(currentAngle - halfAngle, currentAngle + halfAngle);
        }
    }
}