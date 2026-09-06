using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AbilityRunner : MonoBehaviour
{
    [SerializeField] private Ability[] abilities;
    [SerializeField] private float decisionInterval = 0.5f;   // "every once in a while"

    private float[] cooldowns;        // per-enemy, indexed like abilities (assets are shared!)
    private float decisionTimer;
    private Coroutine running;

    public bool IsBusy => running != null;

    private void Awake() => cooldowns = new float[abilities?.Length ?? 0];

    private void Update()
    {
        for (int i = 0; i < cooldowns.Length; i++) if (cooldowns[i] > 0f) cooldowns[i] -= Time.deltaTime;
        if (decisionTimer > 0f) decisionTimer -= Time.deltaTime;
    }

    // INPUT:  ctx
    // OUTPUT: true if an ability was started this call
    // USE:    ArchetypeBehavior.HandleAbilities, once per frame while Engaging
    public bool TryUse(EnemyContext c)
    {
        if (IsBusy || decisionTimer > 0f) return false;
        decisionTimer = decisionInterval;
        for (int i = 0; i < abilities.Length; i++)
        {
            if (cooldowns[i] > 0f || !abilities[i].CanUse(c)) continue;
            cooldowns[i] = abilities[i].cooldown;
            running = StartCoroutine(Run(abilities[i], c));
            return true;
        }
        return false;
    }

    private IEnumerator Run(Ability a, EnemyContext c)
    {
        yield return a.Execute(c);
        running = null;
    }

    private void OnDisable() { if (running != null) StopCoroutine(running); running = null; }
}
