using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] BulletSpawn bulletSpawn;
    [SerializeField] AimController aimController;

    InputAction fireAction;
    Damageable damageable;
    private PlayerMovement playerMovement;
    Camera mainCam;

    void Awake()
    {
        if (bulletSpawn == null) bulletSpawn = GetComponentInChildren<BulletSpawn>();
        if (aimController == null) aimController = GetComponentInChildren<AimController>();
        bulletSpawn.isAutomaticSpawn = false;

        playerMovement = GetComponent<PlayerMovement>();

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

        // no shooting while dashing or during recovery
        if (playerMovement != null && (playerMovement.isDashing || playerMovement.isInRecovery)){
            return;
        }

        HandleShooting();
    }

    private void HandleShooting(){
        if (bulletSpawn == null) return;

        BulletSpawnData data = bulletSpawn.GetCurrentData();
        if (data.holdFire)
        {
            if (fireAction.IsPressed()) bulletSpawn.Fire();
        }
        else
        {
            if (fireAction.WasPressedThisFrame()) bulletSpawn.Fire();
        }
    }

    void Die()
    {
        Debug.Log("Player died");
    }
}