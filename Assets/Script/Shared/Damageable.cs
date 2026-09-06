using UnityEngine;
using System;

public class Damageable : MonoBehaviour
{
    public int maxHp;
    public int currentHp;

    public bool isInvulnerable { get; set; }
    public bool IsDead { get; private set; }

    public event Action OnDeath;
    public event Action<int> OnDamaged;

    [Header("Debug")]
    [SerializeField] private bool logDamage = false;

    // INPUT:  maxHp
    // OUTPUT: currentHp = maxHp
    // USE:    Unity
    void Awake() => currentHp = maxHp;

    // INPUT:  raw damage
    // OUTPUT: reduces HP, raises OnDamaged; raises OnDeath ONCE when HP hits 0
    // USE:    Bullet.OnTriggerEnter2D. REPLACES old TakeDamage (death latch)
    public void TakeDamage(int amount)
    {
        if (isInvulnerable || IsDead) return;

        currentHp -= amount;
        OnDamaged?.Invoke(amount);
        if (logDamage) Debug.Log($"{gameObject.name} took {amount} damage. HP: {currentHp}/{maxHp}");

        if (currentHp <= 0)
        {
            IsDead = true;
            OnDeath?.Invoke();
        }
    }

    // INPUT:  none
    // OUTPUT: current HP
    // USE:    health bars, AI retreat logic
    public int GetHp() => currentHp;
}