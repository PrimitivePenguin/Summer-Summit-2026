using UnityEngine;


// Returns angle between two points in degrees
// Attack to bulletspawner
// Call AimAt() at target
public class AimController : MonoBehaviour
{
    public float GetAngleTo(Vector2 targetPos)
    {

        Vector2 direction = targetPos - (Vector2)transform.position;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg -90f; // bullet.cs uses translate up -> 90 deg mismatch
        // Debug.DrawRay(transform.position, direction.normalized * 3f, Color.red);
        return angle;
    }
    // Set rotation cone from targetPos in center of the cone, with angle from bulletSpawnData
    public void AimAt(Vector2 targetPos, BulletSpawn bulletSpawn)
    {
        float angle = GetAngleTo(targetPos);
        // Debug.Log($"[AimController] Target: {targetPos}, Current Position: {(Vector2)transform.position}, angle: {angle}");
        float halfAngle = bulletSpawn.GetCurrentData().spreadAngle / 2f;
        bulletSpawn.SetFiringArc(angle - halfAngle, angle + halfAngle);
    }
}
