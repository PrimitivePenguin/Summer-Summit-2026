using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public class PlayerUltimate : MonoBehaviour
{
    [SerializeField] private float baseCooldown = 30f;
    [SerializeField] private float cooldownReductionPerKill = 1f;
    [SerializeField] private Slider hudBar;              // fill = readiness, 1 = ready
    [SerializeField] private UnityEvent onUltimate;       // hook the actual effect here later

    private float remaining;

    private void Start()
    {
        remaining = baseCooldown;
        LevelManager.Instance.OnEnemyKilled += HandleKill;
    }

    private void OnDestroy()
    {
        if (LevelManager.Instance != null) LevelManager.Instance.OnEnemyKilled -= HandleKill;
    }

    private void HandleKill()
    {
        remaining = Mathf.Max(0f, remaining - cooldownReductionPerKill);
        Refresh();
    }

    private void Update()
    {
        if (remaining > 0f)
        {
            remaining = Mathf.Max(0f, remaining - Time.deltaTime);
            Refresh();
        }
        else if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            remaining = baseCooldown;
            Refresh();
            onUltimate.Invoke();
        }
    }

    private void Refresh() => hudBar.value = 1f - (remaining / baseCooldown);
}