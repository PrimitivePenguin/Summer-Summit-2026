using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(menuName = "Enemy/Ability/Summon")]
public class SummonAbility : Ability
{
    [System.Serializable] public class Entry { public GameObject prefab; public int maxAlive = 2; public int perCast = 1; }
    [SerializeField] private Entry[] entries;              // e.g. Mob_Chaser max 4, Elite_Skirmisher max 2
    [SerializeField] private float spawnRadius = 3f;

    private int Alive(EnemyContext c, Entry e)
    { c.tracked.RemoveAll(g => g == null); return c.tracked.Count(g => g.name.StartsWith(e.prefab.name)); }

    public override bool CanUse(EnemyContext c) => entries.Any(e => Alive(c, e) < e.maxAlive);

    public override IEnumerator Execute(EnemyContext c)
    {
        var glow = Spawn(telegraphPrefab, c.self.position);
        yield return new WaitForSeconds(telegraphDuration);
        if (glow) Object.Destroy(glow);
        foreach (var e in entries)
            for (int i = 0; i < e.perCast && Alive(c, e) < e.maxAlive; i++)
            {
                Vector2 pos = (Vector2)c.self.position + Random.insideUnitCircle.normalized * spawnRadius;
                var go = Object.Instantiate(e.prefab, pos, Quaternion.identity);
                go.name = e.prefab.name;                       // strip "(Clone)" so the prefix count works
                c.tracked.Add(go);
                go.GetComponent<EnemyVision>()?.AlertToPosition(c.targetPos, 1f);   // summons come out already hunting
            }
    }
}