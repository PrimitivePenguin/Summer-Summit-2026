using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] BulletSpawn bulletSpawn;
    [SerializeField] AimController aimController;

    InputAction fireAction;
    Damageable damageable;
    Camera mainCam;

    void Awake()
    {
        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();
        bulletSpawn.isAutomaticSpawn = false;

        damageable = GetComponent<Damageable>();
        damageable.OnDeath += Die;

        fireAction = new InputAction("Fire", InputActionType.Button, "<Mouse>/LeftButton");
        fireAction.Enable();
    }
    void Start()
    {
        bulletSpawn.isAutomaticSpawn = false;
        mainCam = Camera.main;
    }

    void OnDisable() => fireAction?.Disable();

    void Update()
    {
        Vector2 mousePos = mainCam.ScreenToWorldPoint(Mouse.current.position.ReadValue());
        aimController.AimAt(mousePos, bulletSpawn);

        BulletSpawnData data = bulletSpawn.GetCurrentData();
        if (data.holdFire)
        {
            // Hold
            if (fireAction.IsPressed())
                bulletSpawn.Fire();
        } else
        {
            // Press
            if (fireAction.WasPressedThisFrame())
                bulletSpawn.Fire();
        }
    }

    void Die()
    {
        Debug.Log("Player died");
    }
}