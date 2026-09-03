using UnityEngine;

[RequireComponent(typeof(Damageable))]
public class Breakable : MonoBehaviour
{
    [SerializeField] private GameObject breakEffectPrefab; // optional: particles/rubble

    private Damageable damageable;

    void Awake()
    {
        damageable = GetComponent<Damageable>();
        damageable.OnDeath += Break;
    }

    void OnDestroy()
    {
        damageable.OnDeath -= Break;
    }
    
    private void Break(){
        if (breakEffectPrefab != null)
        {
            Instantiate(breakEffectPrefab, transform.position, Quaternion.identity);
        }

        Destroy(gameObject);
    }
}
