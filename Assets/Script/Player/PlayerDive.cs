using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Collider2D))]
public class PlayerDive : MonoBehaviour
{
    public static bool IsPlayerSubmerged { get; private set; }

    [Header("Air Tank Settings")]
    [SerializeField] private float maxAir = 5f;
    [SerializeField] private float airDepleteRate = 1f;
    [SerializeField] private float airRechargeRate = 1.5f;
    [SerializeField] private float rechargeDelay = 1f;
    [SerializeField] private float diveCost = 1.0f;
    [SerializeField] private float minSubmergedTime = 0.5f;

    [Header("Overhead Air Bar UI")]
    [Tooltip("World Space Slider or Canvas positioned over the player")]
    [SerializeField] private Slider overheadAirSlider;
    [SerializeField] private Vector3 overheadOffset = new Vector3(0f, 0.85f, 0f);
    [SerializeField] private bool hideWhenFull = true;

    [Header("Ambush Strike")]
    [SerializeField] private float ambushRadius = 2.5f;
    [SerializeField] private int ambushDamage = 100;
    [SerializeField] private LayerMask enemyLayerMask;

    [Header("Recoil / Punishment Settings")]
    [SerializeField] private int misfireDamage = 8;
    [SerializeField] private float misfireAlertRadius = 7.0f;

    [Header("Visual Shifting (Submerged)")]
    [Tooltip("The fullscreen black sprite covering the camera (Order in Layer ~50)")]
    [SerializeField] private GameObject submergedBlackout;

    [Tooltip("The TilemapRenderer on your neon green cloned tilemap under Grid (Order in Layer ~60)")]
    [SerializeField] private Renderer neonWallsRenderer;

    [Tooltip("Small green dot sprite/object representing player position (Order in Layer ~55 or 60)")]
    [SerializeField] private GameObject submergedPlayerDot;

    [SerializeField] private SpriteRenderer[] renderersToHide;

    private float currentAir;
    private float rechargeTimer;
    private float submergedTimer;
    private bool isSubmerged;
    private int originalLayer;
    private string originalTag;
    private Collider2D playerCol;
    private Damageable playerDamageable;

    public bool IsSubmerged => isSubmerged;
    public float AirRatio => currentAir / maxAir;
    [Header("Input Mode")]
    [Tooltip("false = press E to dive, press E again to surface. true = hold E to stay under, release to surface.")]
    [SerializeField] private bool holdToDive = false;

    private bool surfaceQueued;
    private void Awake()
    {
        playerCol = GetComponent<Collider2D>();
        playerDamageable = GetComponent<Damageable>();
        originalLayer = gameObject.layer;
        originalTag = gameObject.tag;
        currentAir = maxAir;
        IsPlayerSubmerged = false;

        if (renderersToHide == null || renderersToHide.Length == 0)
            renderersToHide = GetComponentsInChildren<SpriteRenderer>();

        if (submergedBlackout != null)
            submergedBlackout.SetActive(false);

        if (neonWallsRenderer != null)
            neonWallsRenderer.enabled = false;

        if (submergedPlayerDot != null)
            submergedPlayerDot.SetActive(false);

        UpdateOverheadBar(forceUpdate: true);
    }

    // INPUT:  none (called by keyboard shortcut and by DiveButton UI)
    // OUTPUT: dives if surfaced with enough air; surfaces if submerged past min time
    // USE:    Update (E key), DiveButton.OnPressed
    public void ToggleDive()
    {
        if (Time.timeScale == 0f) return;   // ignore clicks while paused/cutscene

        if (!isSubmerged && currentAir >= diveCost)
            Dive();
        else if (isSubmerged && submergedTimer <= 0f)
            Surface(forcedByAirDepletion: false);
    }

    // INPUT:  none
    // OUTPUT: true if pressing dive right now would do something
    // USE:    DiveButton visual state (translucency)
    public bool CanToggleDive => isSubmerged ? submergedTimer <= 0f : currentAir >= diveCost;

    
    // INPUT:  E key state, holdToDive mode
    // OUTPUT: dive/surface routed by mode; queued surface honored once min-submerge elapses
    // USE:    Unity
    private void Update()
    {
        if (isSubmerged && submergedTimer > 0f)
            submergedTimer -= Time.deltaTime;

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (!holdToDive)
            {
                if (kb.eKey.wasPressedThisFrame) ToggleDive();
            }
            else
            {
                if (kb.eKey.wasPressedThisFrame && !isSubmerged && currentAir >= diveCost)
                    Dive();
                if (kb.eKey.wasReleasedThisFrame && isSubmerged)
                    surfaceQueued = true;   // may still be inside min-submerge — queue it
            }
        }

        // Hold mode: surface the moment the min-submerge window opens
        if (surfaceQueued && isSubmerged && submergedTimer <= 0f)
        {
            surfaceQueued = false;
            Surface(forcedByAirDepletion: false);
        }
        if (!isSubmerged) surfaceQueued = false;   // stale queue guard

        HandleAir();
    }

    // private void LateUpdate()
    // {
    //     // Keep overhead bar pinned above the player and upright (ignores player sprite rotation)
    //     if (overheadAirSlider != null)
    //     {
    //         overheadAirSlider.transform.position = transform.position + overheadOffset;
    //         overheadAirSlider.transform.rotation = Quaternion.identity;
    //     }
    // }

    private void HandleAir()
    {
        if (isSubmerged)
        {
            currentAir -= airDepleteRate * Time.deltaTime;
            rechargeTimer = rechargeDelay;

            if (currentAir <= 0f)
            {
                currentAir = 0f;
                Surface(forcedByAirDepletion: true);
            }
        }
        else
        {
            if (rechargeTimer > 0f)
            {
                rechargeTimer -= Time.deltaTime;
            }
            else if (currentAir < maxAir)
            {
                currentAir += airRechargeRate * Time.deltaTime;
                if (currentAir > maxAir) currentAir = maxAir;
            }
        }

        UpdateOverheadBar();
    }

    private void UpdateOverheadBar(bool forceUpdate = false)
    {
        if (overheadAirSlider == null) return;

        float ratio = currentAir / maxAir;
        overheadAirSlider.value = ratio;

        if (hideWhenFull)
        {
            bool shouldShow = isSubmerged || ratio < 0.999f;
            if (overheadAirSlider.gameObject.activeSelf != shouldShow || forceUpdate)
            {
                overheadAirSlider.gameObject.SetActive(shouldShow);
            }
        }
        else if (!overheadAirSlider.gameObject.activeSelf)
        {
            overheadAirSlider.gameObject.SetActive(true);
        }
    }

    private void Dive()
    {
        isSubmerged = true;
        IsPlayerSubmerged = true;
        submergedTimer = minSubmergedTime;

        currentAir = Mathf.Max(0f, currentAir - diveCost);

        foreach (var r in renderersToHide)
        {
            // Do NOT hide the green dot, nor any graphics belonging to the overhead bar
            if (r == null) continue;
            if (submergedPlayerDot != null && r.gameObject == submergedPlayerDot) continue;
            if (overheadAirSlider != null && r.transform.IsChildOf(overheadAirSlider.transform)) continue;

            r.enabled = false;
        }

        if (submergedPlayerDot != null)
            submergedPlayerDot.SetActive(true);

        if (submergedBlackout != null)
            submergedBlackout.SetActive(true);

        if (neonWallsRenderer != null)
            neonWallsRenderer.enabled = true;

        gameObject.tag = "Untagged";
        gameObject.layer = LayerMask.NameToLayer("Ignore Raycast");
        if (playerCol != null) playerCol.enabled = false;

        UpdateOverheadBar();
    }

    private void Surface(bool forcedByAirDepletion)
    {
        if (forcedByAirDepletion)
        {
            Debug.Log("[PlayerDive] Air depleted while submerged!");
            ApplyRecoilPenalty();
        }
        else
        {
            ExecuteAmbushBurst();
        }

        isSubmerged = false;
        IsPlayerSubmerged = false;

        if (submergedPlayerDot != null)
            submergedPlayerDot.SetActive(false);

        if (submergedBlackout != null)
            submergedBlackout.SetActive(false);

        if (neonWallsRenderer != null)
            neonWallsRenderer.enabled = false;

        foreach (var r in renderersToHide)
            if (r != null) r.enabled = true;

        gameObject.tag = originalTag;
        gameObject.layer = originalLayer;
        if (playerCol != null) playerCol.enabled = true;

        UpdateOverheadBar();
    }

    private void ExecuteAmbushBurst()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, ambushRadius, enemyLayerMask);
        bool landedAmbush = false;

        foreach (var col in hits)
        {
            EnemyVision vision = col.GetComponent<EnemyVision>() ?? col.GetComponentInParent<EnemyVision>();
            bool enemyDoesNotSeeMe = (vision == null || !vision.canSeePlayer);

            if (enemyDoesNotSeeMe)
            {
                Damageable dmg = col.GetComponent<Damageable>() ?? col.GetComponentInParent<Damageable>();
                if (dmg != null)
                {
                    dmg.TakeDamage(ambushDamage);
                    landedAmbush = true;
                    Debug.Log($"[PlayerDive] Ambushed {col.name} for {ambushDamage} damage!");
                }
            }
        }

        if (!landedAmbush)
        {
            ApplyRecoilPenalty();
        }
    }

    private void ApplyRecoilPenalty()
    {
        if (playerDamageable == null)
            playerDamageable = GetComponent<Damageable>() ?? GetComponentInParent<Damageable>();

        if (playerDamageable != null)
        {
            playerDamageable.TakeDamage(misfireDamage);
            Debug.Log($"[PlayerDive] Whiffed/Drowned: Took {misfireDamage} recoil damage!");
        }

        AlertNearbyEnemies();
    }

    private void AlertNearbyEnemies()
    {
        Collider2D[] alertHits = Physics2D.OverlapCircleAll(transform.position, misfireAlertRadius, enemyLayerMask);
        foreach (var col in alertHits)
        {
            var vision = col.GetComponent<EnemyVision>() ?? col.GetComponentInParent<EnemyVision>();
            if (vision != null)
            {
                vision.AlertToPosition(transform.position, 1.0f);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, ambushRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, misfireAlertRadius);
    }
}