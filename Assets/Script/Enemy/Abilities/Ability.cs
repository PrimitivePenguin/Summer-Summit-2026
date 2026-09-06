using UnityEngine;
using System.Collections;
using System.Collections.Generic;


public abstract class Ability : ScriptableObject
{
    [Header("Timing")]
    public float cooldown = 4f;
    public float telegraphDuration = 0.8f;   // warning window before the effect lands
    [Header("Gating")]
    public float maxRange = 8f;              // vs ctx.distToTarget
    public bool requiresContact = true;      // vision.hasContact must be true
    [Header("Presentation")]
    public GameObject telegraphPrefab;       // artist placeholder (circle, laser, glow)
    public GameObject effectPrefab;          // artist placeholder (kaboom, heal burst)

    // INPUT:  ctx (targetPos, distToTarget, vision, etc.)
    // OUTPUT: true if this ability should be considered right now
    // USE:    AbilityRunner.TryUse. Override for ability-specific gates (allies hurt, summon cap)
    public virtual bool CanUse(EnemyContext c)
        => c.distToTarget <= maxRange && (!requiresContact || c.vision.hasContact);

    // INPUT:  ctx
    // OUTPUT: coroutine; runner marks IsBusy for its duration
    // USE:    AbilityRunner starts it. Must yield at least once if it telegraphs.
    public abstract IEnumerator Execute(EnemyContext c);

    protected GameObject Spawn(GameObject prefab, Vector2 pos, float lifetime = 0f)
    {
        if (prefab == null) return null;
        var go = Object.Instantiate(prefab, pos, Quaternion.identity);
        if (lifetime > 0f) Object.Destroy(go, lifetime);
        return go;
    }
}