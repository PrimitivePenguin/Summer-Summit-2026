using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WaveController : MonoBehaviour
{
    public enum WaveState { Idle, Spawning, Fighting, Between, Complete }

    [Header("Refs")]
    [SerializeField] private EnemySpawner spawner;

    [Header("Waves (authored per level)")]
    [SerializeField] private WaveData[] waves;

    [Header("Timing")]
    [SerializeField] private bool autoStart = true;
    [SerializeField] private float firstWaveDelay = 2f;

    public event Action<int> OnWaveStarted;       // wave index
    public event Action<int> OnWaveCleared;       // wave index
    public event Action OnAllWavesComplete;

    public WaveState State { get; private set; } = WaveState.Idle;
    public int CurrentWave { get; private set; } = -1;
    public int WaveCount => waves != null ? waves.Length : 0;

    private int alive;
    private LevelManager level;
    private bool registeredAsSource;

    // INPUT:  LevelManager.Instance, spawner
    // OUTPUT: subscribed to alive count; registered as a pending spawn source; begins if autoStart
    // USE:    Unity
    private void Start()
    {
        level = LevelManager.Instance;
        if (spawner == null) spawner = FindAnyObjectByType<EnemySpawner>();
        if (level == null || spawner == null) { Debug.LogError("[WaveController] Missing LevelManager or EnemySpawner"); enabled = false; return; }

        level.OnEnemiesRemainingChanged += HandleAliveChanged;
        RegisterSource();                   // hold EliminateAll open until the last wave has spawned
        if (autoStart) Begin();
    }

    private void OnDestroy()
    {
        if (level != null) level.OnEnemiesRemainingChanged -= HandleAliveChanged;
    }

    // INPUT:  none
    // OUTPUT: wave 0 starts after firstWaveDelay. No-op if already running.
    // USE:    Start (autoStart), BossArenaTrigger.Lock, cutscene end, debug key
    public void Begin()
    {
        if (State != WaveState.Idle) return;
        RegisterSource();
        StartCoroutine(BeginAfter(firstWaveDelay, 0));
    }

    // INPUT:  delay, wave index
    // OUTPUT: State = Between during the wait, then BeginWave
    // USE:    Begin, CheckCleared
    private IEnumerator BeginAfter(float delay, int index)
    {
        State = WaveState.Between;
        yield return new WaitForSeconds(delay);
        BeginWave(index);
    }

    // INPUT:  wave index
    // OUTPUT: State = Spawning with all group coroutines running; Complete if index is past the end
    // USE:    BeginAfter
    private void BeginWave(int index)
    {
        if (level.IsLevelEnded) return;

        if (index >= WaveCount)
        {
            State = WaveState.Complete;
            OnAllWavesComplete?.Invoke();
            return;
        }

        CurrentWave = index;
        State = WaveState.Spawning;
        OnWaveStarted?.Invoke(index);
        StartCoroutine(RunWave(waves[index]));
    }

    // INPUT:  wave
    // OUTPUT: all groups spawned; State = Fighting; spawn source released after the final wave
    // USE:    BeginWave
    private IEnumerator RunWave(WaveData wave)
    {
        var handles = new List<Coroutine>();
        foreach (SpawnGroup g in wave.groups)
            handles.Add(StartCoroutine(RunGroup(g, wave.maxAliveAtOnce)));
        foreach (Coroutine h in handles) yield return h;

        // One frame so the last Instantiate's Start() has run and RegisterEnemy counted it.
        // Without this, alive can still read 0 here and the wave "clears" instantly.
        yield return null;

        State = WaveState.Fighting;
        if (CurrentWave == WaveCount - 1) UnregisterSource();   // nothing left to spawn, ever
        CheckCleared();
    }

    // INPUT:  group, alive cap
    // OUTPUT: group.count enemies spawned, throttled by cap, retrying when no legal point exists
    // USE:    RunWave
    private IEnumerator RunGroup(SpawnGroup g, int maxAlive)
    {
        yield return new WaitForSeconds(g.startDelay);

        for (int i = 0; i < g.count; i++)
        {
            while (maxAlive > 0 && alive >= maxAlive) yield return null;
            if (level.IsLevelEnded) yield break;

            if (spawner.Spawn(g.prefab, g.spawnGroup, g.route) == null)
            {
                yield return new WaitForSeconds(0.25f);   // player camping every point — try again
                i--;
                continue;
            }

            if (g.spacing > 0f) yield return new WaitForSeconds(g.spacing);
        }
    }

    // INPUT:  alive count from LevelManager
    // OUTPUT: cached; may clear the wave
    // USE:    LevelManager.OnEnemiesRemainingChanged
    private void HandleAliveChanged(int n)
    {
        alive = n;
        CheckCleared();
    }

    // INPUT:  State, alive
    // OUTPUT: if Fighting and nobody alive → OnWaveCleared, schedule next wave
    // USE:    HandleAliveChanged, RunWave
    private void CheckCleared()
    {
        if (State != WaveState.Fighting || alive > 0) return;
        OnWaveCleared?.Invoke(CurrentWave);
        StartCoroutine(BeginAfter(waves[CurrentWave].postWaveDelay, CurrentWave + 1));
    }

    private void RegisterSource()   { if (registeredAsSource) return; registeredAsSource = true;  level.RegisterSpawnSource(); }
    private void UnregisterSource() { if (!registeredAsSource) return; registeredAsSource = false; level.UnregisterSpawnSource(); }
}