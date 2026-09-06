using System;
using System.Collections.Generic;
using UnityEngine;

public enum WinConditionType
{
    EliminateAll,
    SurviveTime,
    ReachFlag,
    KillTarget
}

// Win/loss tracking for one level. Declared PARTIAL so future systems live in
// their own files (see LevelManager.Future.cs) without touching this one.
public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Win Condition Selection")]
    [SerializeField] private WinConditionType winCondition = WinConditionType.EliminateAll;

    [Header("Survive Settings (Condition 2)")]
    [SerializeField] private float surviveDuration = 60f;
    public float SurviveDuration => surviveDuration;
    private float timeRemaining;

    [Header("Flag Extraction (Condition 3)")]
    [SerializeField] private ExtractionFlag targetFlag;

    [Header("Kill Target (Condition 4)")]
    [SerializeField] private Damageable killTarget;    // may be null until BossArenaTrigger sets it
    public void SetKillTarget(Damageable d)
    {
        if (killTarget != null) killTarget.OnDeath -= TriggerVictory;
        killTarget = d; if (d != null) d.OnDeath += TriggerVictory;
    }

    public event Action<WinConditionType> OnLevelWon;
    public event Action OnLevelLost;
    public event Action<int> OnEnemiesRemainingChanged;
    public event Action<float> OnTimerTick;
    public event System.Action OnEnemyKilled;


    public bool IsLevelEnded { get; private set; }
    private int enemyCount = 0;
    private Damageable playerDamageable;

    // INPUT:  none
    // OUTPUT: singleton assignment
    // USE:    Unity
    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void Start()
    {
        timeRemaining = surviveDuration;

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerDamageable = player.GetComponent<Damageable>();
            if (playerDamageable != null)
                playerDamageable.OnDeath += HandlePlayerDeath;
        }

        if (winCondition == WinConditionType.ReachFlag && targetFlag != null)
        {
            targetFlag.OnFlagReached += TriggerVictory;
        }

        if (winCondition == WinConditionType.KillTarget && killTarget != null) SetKillTarget(killTarget);

        // Notify UI of initial enemy count once everyone has registered
        OnEnemiesRemainingChanged?.Invoke(enemyCount);

        OnLevelStarted();   // stub below — fill in LevelManager.Future.cs
    }

    private void Update()
    {
        if (IsLevelEnded) return;

        if (winCondition == WinConditionType.SurviveTime)
        {
            timeRemaining -= Time.deltaTime;
            OnTimerTick?.Invoke(Mathf.Max(0f, timeRemaining));

            if (timeRemaining <= 0f)
            {
                TriggerVictory();
            }
        }
    }

    private void OnDestroy()
    {
        if (playerDamageable != null)
            playerDamageable.OnDeath -= HandlePlayerDeath;

        if (targetFlag != null)
            targetFlag.OnFlagReached -= TriggerVictory;
        if (killTarget != null)
            killTarget.OnDeath -= TriggerVictory;
    }

    // --- ENEMY TRACKING METHODS ---

    // INPUT:  none
    // OUTPUT: enemyCount + 1, UI event
    // USE:    EnemyController.Start
    public void RegisterEnemy()
    {
        enemyCount++;
        OnEnemiesRemainingChanged?.Invoke(enemyCount);
    }

    // INPUT:  none
    // OUTPUT: enemyCount - 1, UI event, victory if EliminateAll hits 0
    // USE:    EnemyController.HandleDeath (now guaranteed once per enemy by Damageable)
    public void UnregisterEnemy()
    {
    
        if (IsLevelEnded) return;

        enemyCount = Mathf.Max(0, enemyCount - 1);
        OnEnemiesRemainingChanged?.Invoke(enemyCount);
        OnEnemyKilled?.Invoke();
    
        if (winCondition == WinConditionType.EliminateAll && enemyCount == 0)
        {
            TriggerVictory();
        }
    }
    // --- WIN / LOSS TRIGGERS ---

    // INPUT:  none
    // OUTPUT: latches IsLevelEnded, raises OnLevelWon
    // USE:    all win paths
    public void TriggerVictory()
    {
        if (IsLevelEnded) return;
        IsLevelEnded = true;

        Debug.Log($"<color=green>[LevelManager] VICTORY! Win condition achieved: {winCondition}</color>");
        OnLevelWon?.Invoke(winCondition);
        OnLevelEnded(true);   // future-hook slot
    }

    private void HandlePlayerDeath()
    {
        if (IsLevelEnded) return;
        IsLevelEnded = true;

        Debug.Log("<color=red>[LevelManager] DEFEAT! Player was destroyed.</color>");
        OnLevelLost?.Invoke();
        OnLevelEnded(false);  // stub below
    }

    // ── Future expansion hooks ────────────────────────────────────────────────
    // Stubs live here so LevelManager.cs compiles alone. Override behaviour in
    // LevelManager.Future.cs by adding public methods and wiring them to your
    // systems from outside — do not add private calls there.

    // INPUT:  none
    // OUTPUT: none — called once at the end of Start
    // USE:    difficulty setup, spawn waves, etc.
    private void OnLevelStarted() { /* TODO */ }

    // INPUT:  won = true on victory, false on defeat
    // OUTPUT: none — called from TriggerVictory / HandlePlayerDeath
    // USE:    level unlock persistence, scene transition, analytics
    private void OnLevelEnded(bool won) { /* TODO */ }
}