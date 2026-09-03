using UnityEngine;


// Returns angle between two points in degrees
// Attack to bulletspawner
// Call AimAt() at target
public class AimController : MonoBehaviour
{
    [Header("Turn")]
    public bool isTurnRate = true;
    public float turnRate; // degrees per second
    private float currentAngle;
    public float GetFacingAngle() => currentAngle;

    // called by EnemyController.cs with target position, requires EnemyData -> implement later
    // public void Initialize(float turnRate)
    // {
    //     this.turnRate = isTurnRate ? turnRate : Mathf.Infinity; // if turnRate is 0, set to infinity
    // }
    // Awake() replaces Initialize
    void Awake()
    {
        currentAngle = transform.eulerAngles.z;
        turnRate = isTurnRate ? turnRate : Mathf.Infinity; // if turnRate is 0, set to infinity
    }


    // Raw angle calc
    public float GetAngleTo(Vector2 targetPos)
    {
        Vector2 direction = targetPos - (Vector2)transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg -90f; // bullet.cs uses translate up -> 90 deg mismatch
        return angle;
    }

    // Set rotation cone from targetPos in center of the cone, with angle from bulletSpawnData
    public void AimAt(Vector2 targetPos, BulletSpawn bulletSpawn)
    {
        float targetAngle = GetAngleTo(targetPos);
        // change current angle by time * tunrate based on current + target angle
        // rotate object to face target position
        if (!isTurnRate){
            currentAngle = targetAngle;
        }
        else if (Time.deltaTime > 0f){
            currentAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, turnRate * Time.deltaTime);
        }
        if (float.IsNaN(currentAngle)){
            currentAngle = targetAngle;
        }
        transform.rotation = Quaternion.Euler(0, 0, currentAngle);

        if (bulletSpawn != null){
            // Debug.Log($"[AimController] Target: {targetPos}, Current Position: {(Vector2)transform.position}, angle: {targetAngle}");
            float halfAngle = bulletSpawn.GetCurrentData().spreadAngle / 2f;
            bulletSpawn.SetFiringArc(currentAngle - halfAngle, currentAngle + halfAngle);
        }
    }
    // aim to snap
    public void SnapTo(float angle)
    {
        currentAngle = angle;
        transform.rotation = Quaternion.Euler(0, 0, currentAngle); 
    }
    public bool IsAimedAt(Vector2 targetPos, float tolerance = 5f)
    {
        float targetAngle = GetAngleTo(targetPos);
        return Mathf.Abs(Mathf.DeltaAngle(currentAngle, targetAngle)) < tolerance;
    }
}
