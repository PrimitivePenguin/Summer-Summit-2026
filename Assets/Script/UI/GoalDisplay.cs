using UnityEngine;
using TMPro;

public class GoalDisplay : MonoBehaviour
{
    // INPUT:  goalText — drag a TextMeshProUGUI here; killTargetName typed per scene
    // OUTPUT: goal line kept current from LevelManager events (no per-frame polling)
    // USE:    attach to a HUD text object, top-center or under the top-left bars

    [SerializeField] private TMP_Text goalText;
    [Tooltip("Shown for KillTarget levels, e.g. 'the Warden'. Falls back to 'the boss'.")]
    [SerializeField] private string killTargetName = "the boss";

    private int enemiesLeft;

    // INPUT:  LevelManager singleton
    // OUTPUT: subscribed to count/timer/win events, initial line drawn
    // USE:    Unity. Start (not Awake) so LevelManager.Awake has assigned Instance.
    private void Start()
    {
        if (goalText == null) goalText = GetComponent<TMP_Text>();
        var lm = LevelManager.Instance;
        if (lm == null) { gameObject.SetActive(false); return; }

        lm.OnEnemiesRemainingChanged += HandleEnemyCount;
        lm.OnTimerTick += HandleTimer;
        lm.OnLevelWon += HandleWon;

        Redraw(lm.SurviveDuration);
    }

    // INPUT:  none
    // OUTPUT: unsubscribed
    // USE:    Unity
    private void OnDestroy()
    {
        var lm = LevelManager.Instance;
        if (lm == null) return;
        lm.OnEnemiesRemainingChanged -= HandleEnemyCount;
        lm.OnTimerTick -= HandleTimer;
        lm.OnLevelWon -= HandleWon;
    }

    // INPUT:  current enemy count (LevelManager event)
    // OUTPUT: cached + line redrawn
    // USE:    event
    private void HandleEnemyCount(int count) { enemiesLeft = count; Redraw(0f); }

    // INPUT:  seconds remaining (LevelManager event, SurviveTime only)
    // OUTPUT: line redrawn with mm:ss
    // USE:    event
    private void HandleTimer(float secondsLeft) => Redraw(secondsLeft);

    // INPUT:  win condition (unused)
    // OUTPUT: completion line
    // USE:    LevelManager.OnLevelWon event
    private void HandleWon(WinConditionType _) => goalText.text = "Goal complete!";

    // INPUT:  seconds for the survive case; enemiesLeft cached from events
    // OUTPUT: goalText set per win condition
    // USE:    all handlers — single formatting funnel
    private void Redraw(float secondsLeft)
    {
        switch (LevelManager.Instance.WinCondition)
        {
            case WinConditionType.EliminateAll:
                goalText.text = $"Eliminate all enemies — {enemiesLeft} left";
                break;
            case WinConditionType.SurviveTime:
                int m = Mathf.FloorToInt(secondsLeft / 60f);
                int s = Mathf.FloorToInt(secondsLeft % 60f);
                goalText.text = $"Survive — {m}:{s:00}";
                break;
            case WinConditionType.ReachFlag:
                goalText.text = "Reach the extraction point";
                break;
            case WinConditionType.KillTarget:
                goalText.text = $"Kill {killTargetName}";
                break;
        }
    }
}