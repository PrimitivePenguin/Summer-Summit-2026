using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Enemy/Ability/Telegraphed Shot")]
public class TelegraphedShotAbility : Ability
{
    [SerializeField] private float laserLength = 30f;

    public override bool CanUse(EnemyContext c)
        => base.CanUse(c) && c.aim.IsAimedAt(c.targetPos, 3f);

    public override IEnumerator Execute(EnemyContext c)
    {
        var laser = Spawn(telegraphPrefab, c.self.position);         // prefab: LineRenderer, 2 points
        var lr = laser ? laser.GetComponent<LineRenderer>() : null;
        float t = 0f;
        while (t < telegraphDuration)
        {
            c.aim.AimAt(c.targetPos);                                  // keeps tracking during wind-up
            if (lr) { lr.SetPosition(0, c.self.position); lr.SetPosition(1, c.self.position + c.self.up * laserLength); }
            t += Time.deltaTime; yield return null;
        }
        if (laser) Object.Destroy(laser);
        c.bulletSpawn.Fire();                                          // one EnemySniper burst
    }
}