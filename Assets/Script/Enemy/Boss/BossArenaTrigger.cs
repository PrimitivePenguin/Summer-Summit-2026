using System;
using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

// Locks the player into an arena, wakes the boss, optionally releases on boss death.
// Makes NO assumption about the win condition — that stays in LevelManager.
public class BossArenaTrigger : MonoBehaviour
{
    [Header("Arena")]
    [Tooltip("Polygon covering the arena floor. Doubles as the Cinemachine confiner shape.")]
    [SerializeField] private PolygonCollider2D arenaBounds;
    [Tooltip("Inactive objects on the Collision layer that seal the exits. Activated on lock.")]
    [SerializeField] private GameObject[] doorBlockers;
    [SerializeField] private bool unlockOnBossDeath = true;

    [Header("Boss")]
    [Tooltip("Inactive in the scene until lock. Must have Damageable + EnemyController.")]
    [SerializeField] private GameObject boss;
    [Tooltip("Optional. A WaveController with autoStart = false for adds during the fight.")]
    [SerializeField] private WaveController arenaWaves;

    [Header("Intro")]
    [SerializeField] private TMPro.TMP_Text bossLine;
    [SerializeField] private float lineDuration = 3f;

    public event Action OnLocked;
    public event Action OnUnlocked;
    public bool IsLocked { get; private set; }

    private Transform player;
    private CinemachineConfiner2D confiner;
    private Collider2D previousBounds;
    private Damageable bossDamageable;
    private bool fired;

    // INPUT:  scene
    // OUTPUT: refs cached, doors down, boss asleep, level told a boss is still coming
    // USE:    Unity
    private void Start()
    {
        Player p = FindAnyObjectByType<Player>();
        if (p != null) player = p.transform;

        confiner = FindAnyObjectByType<CinemachineConfiner2D>();
        bossDamageable = boss != null ? boss.GetComponent<Damageable>() : null;

        foreach (GameObject d in doorBlockers) if (d != null) d.SetActive(false);
        if (boss != null) boss.SetActive(false);

        // The boss is inactive, so EnemyController.Start hasn't called RegisterEnemy.
        // Hold EliminateAll open until it wakes (released one frame after Lock).
        LevelManager.Instance?.RegisterSpawnSource();
    }

    // INPUT:  player position, arenaBounds
    // OUTPUT: Lock() on first frame the player is inside
    // USE:    Unity. Polling instead of OnTriggerEnter2D so it works while the player is submerged.
    private void Update()
    {
        if (fired || player == null || arenaBounds == null) return;
        if (arenaBounds.OverlapPoint(player.position)) Lock();
    }

    // INPUT:  none
    // OUTPUT: doors up, pathing grid dirtied, camera confined, boss active + kill target, adds started
    // USE:    Update; also callable from a cutscene or debug key
    public void Lock()
    {
        if (fired) return;
        fired = true;
        IsLocked = true;

        foreach (GameObject d in doorBlockers) if (d != null) d.SetActive(true);
        GridManager.Instance?.MarkDirty();

        if (confiner != null)
        {
            previousBounds = confiner.BoundingShape2D;
            confiner.BoundingShape2D = arenaBounds;
        }

        if (boss != null)
        {
            boss.SetActive(true);                                // EnemyController.Start → RegisterEnemy next frame
            LevelManager.Instance?.SetKillTarget(bossDamageable); // no-op unless winCondition == KillTarget
            if (unlockOnBossDeath && bossDamageable != null) bossDamageable.OnDeath += Unlock;
        }

        if (arenaWaves != null) arenaWaves.Begin();
        if (bossLine != null) StartCoroutine(ShowLine());

        StartCoroutine(ReleaseSourceNextFrame());
        OnLocked?.Invoke();
    }

    // INPUT:  none
    // OUTPUT: spawn source released AFTER the boss has registered itself
    // USE:    Lock. Releasing in the same frame would let EliminateAll fire on a still-uncounted boss.
    private IEnumerator ReleaseSourceNextFrame()
    {
        yield return null;
        LevelManager.Instance?.UnregisterSpawnSource();
    }

    // INPUT:  none
    // OUTPUT: doors down, grid dirtied, camera bounds restored
    // USE:    boss Damageable.OnDeath (if unlockOnBossDeath); also callable manually
    public void Unlock()
    {
        if (!IsLocked) return;
        IsLocked = false;

        if (bossDamageable != null) bossDamageable.OnDeath -= Unlock;

        foreach (GameObject d in doorBlockers) if (d != null) d.SetActive(false);
        GridManager.Instance?.MarkDirty();

        if (confiner != null && previousBounds != null) confiner.BoundingShape2D = previousBounds;

        OnUnlocked?.Invoke();
    }

    private IEnumerator ShowLine()
    {
        bossLine.gameObject.SetActive(true);
        yield return new WaitForSeconds(lineDuration);
        bossLine.gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (bossDamageable != null) bossDamageable.OnDeath -= Unlock;
    }
}