using UnityEngine;
using System;

public class Damageable : MonoBehaviour
{
    public int maxHp;
    int currentHp;

    // invincibility
    public bool isInvulnerable {get; set;}
    public event Action OnDeath;
    public event Action<int> OnDamaged; // passes damage amount

    void Start()
    {
        currentHp = maxHp;
    }

    public void TakeDamage(int amount)
    {
        if (isInvulnerable) return;
        currentHp -= amount;
        OnDamaged?.Invoke(amount);
        Debug.Log($"{gameObject.name} took {amount} damage. HP: {currentHp}/{maxHp}");

        if (currentHp <= 0)
            OnDeath?.Invoke();
    }
}