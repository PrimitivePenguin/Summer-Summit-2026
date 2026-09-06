using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Enemy/Ability/Dash")]
public class DashAbility : Ability
{
    [SerializeField] private float dashSpeed = 14f, dashDuration = 0.2f;
    [SerializeField] private bool sideways = true;         // perpendicular to the player = dodge; false = lunge
    public override IEnumerator Execute(EnemyContext c)
    {
        Vector2 to = (c.targetPos - (Vector2)c.self.position).normalized;
        Vector2 dir = sideways ? (c.strafeClockwise ? new Vector2(-to.y, to.x) : new Vector2(to.y, -to.x)) : to;
        c.damageable.isInvulnerable = true;
        float t = 0f;
        while (t < dashDuration) { c.movement.Move(dir, dashSpeed); t += Time.deltaTime; yield return null; }
        c.damageable.isInvulnerable = false;
    }
}