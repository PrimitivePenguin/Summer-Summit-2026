using System;
using System.Collections.Generic;
using UnityEngine;

public enum WinConditionType
{
    EliminateAll,
    SurviveTime,
    ReachFlag
}

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Win Condition Selection")]
    [SerializeField] private WinConditionType winCondition = WinConditionType.EliminateAll;

    [Header("Survive Settings (Condition 2)")]
    [SerializeField] private float surviveDuration = 60f;
    private float timeRemaining;

    [Header("Flag Extraction (Condition 3)")]
    [SerializeField] private ExtractionFlag targetFlag;

    public event Action<WinConditionType> OnLevelWon;
    public event Action OnLevelLost;
    public event Action<int> OnEnemiesRemainingChanged;
    public event Action<float> OnTimerTick;

    public bool IsLevelEnded { get; private set; }
    private int enemyCount = 0;
    private Damageable playerDamageable;

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

        // Notify UI of initial enemy count once everyone has registered
        OnEnemiesRemainingChanged?.Invoke(enemyCount);
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
    }

    // --- ENEMY TRACKING METHODS ---

    public void RegisterEnemy()
    {
        enemyCount++;
        OnEnemiesRemainingChanged?.Invoke(enemyCount);
    }

    public void UnregisterEnemy()
    {
        if (IsLevelEnded) return;

        enemyCount = Mathf.Max(0, enemyCount - 1);
        OnEnemiesRemainingChanged?.Invoke(enemyCount);

        if (winCondition == WinConditionType.EliminateAll && enemyCount == 0)
        {
            TriggerVictory();
        }
    }

    // --- WIN / LOSS TRIGGERS ---

    public void TriggerVictory()
    {
        if (IsLevelEnded) return;
        IsLevelEnded = true;

        Debug.Log($"<color=green>[LevelManager] VICTORY! Win condition achieved: {winCondition}</color>");
        OnLevelWon?.Invoke(winCondition);
    }

    private void HandlePlayerDeath()
    {
        if (IsLevelEnded) return;
        IsLevelEnded = true;

        Debug.Log("<color=red>[LevelManager] DEFEAT! Player was destroyed.</color>");
        OnLevelLost?.Invoke();
    }
}