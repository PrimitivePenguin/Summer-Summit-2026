using UnityEngine;
using System.Collections;
using System.Collections.Generic;
    

[CreateAssetMenu(menuName = "Enemy/Ability/Artillery Strike")]
public class ArtilleryStrikeAbility : Ability
{
    [SerializeField] private float radius = 2f;
    [SerializeField] private int damage = 6;
    [SerializeField] private LayerMask damageLayers;       // Player + Collision (breaks crates)

    public override IEnumerator Execute(EnemyContext c)
    {
        Vector2 impact = c.targetPos;                              // snapshot: player can dodge
        var warn = Spawn(telegraphPrefab, impact);                 // prefab: red circle sprite, scale = radius*2
        if (warn) warn.transform.localScale = Vector3.one * radius * 2f;
        yield return new WaitForSeconds(telegraphDuration);
        if (warn) Object.Destroy(warn);
        foreach (var h in Physics2D.OverlapCircleAll(impact, radius, damageLayers))
            h.GetComponentInParent<Damageable>()?.TakeDamage(damage);
        Spawn(effectPrefab, impact, 2f);
    }
}