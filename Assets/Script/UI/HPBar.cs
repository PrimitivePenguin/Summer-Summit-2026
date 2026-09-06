using UnityEngine;
using UnityEngine.UI;

public class HpBar : MonoBehaviour
{
    // INPUT:  damageable — drag the Player's Damageable component here in inspector
    // OUTPUT: Slider fill tracks currentHp / maxHp
    // USE:    attach to the HP Slider on the HUD Canvas

    [SerializeField] private Damageable damageable;
    private Slider slider;

    // INPUT:  none
    // OUTPUT: slider configured as a non-interactive 0-1 gauge
    // USE:    Unity
    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.interactable = false;
    }

    // INPUT:  none
    // OUTPUT: initial fill set, subscribed to Damageable's C# events (OnDamaged/OnDeath)
    // USE:    Unity
    private void Start()
    {
        UpdateBar();
        damageable.OnDamaged += HandleDamaged;
        damageable.OnDeath += HandleDeath;
    }

    // INPUT:  none
    // OUTPUT: unsubscribed — prevents events firing into a destroyed UI object on scene change
    // USE:    Unity
    private void OnDestroy()
    {
        if (damageable == null) return;
        damageable.OnDamaged -= HandleDamaged;
        damageable.OnDeath -= HandleDeath;
    }

    // INPUT:  damage amount (ignored — bar reads HP directly from Damageable)
    // OUTPUT: refreshed fill
    // USE:    Damageable.OnDamaged event
    private void HandleDamaged(int _) => UpdateBar();

    // INPUT:  none
    // OUTPUT: slider.value = currentHp / maxHp
    // USE:    Start, HandleDamaged
    private void UpdateBar() => slider.value = (float)damageable.GetHp() / damageable.maxHp;

    // INPUT:  none
    // OUTPUT: bar slammed to zero
    // USE:    Damageable.OnDeath event
    private void HandleDeath() => slider.value = 0f;
}