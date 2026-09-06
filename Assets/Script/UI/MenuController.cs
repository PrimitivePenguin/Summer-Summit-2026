using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject pausePanel;     // "PAUSE" panel: Resume / Restart / Home
    public GameObject gameOverPage;   // defeat: Restart / Home
    public GameObject victoryPage;    // victory: Next / Restart / Home
    public GameObject pauseButton;    // on-screen pause toggle (stays visible while paused)

    [Header("Victory Flow")]
    [Tooltip("Scene loaded by the Next button. LEAVE EMPTY on the last level (L3) — the Next button auto-hides.")]
    [SerializeField] private string nextSceneName = "";
    [Tooltip("The Next button object on the victory page. Hidden when nextSceneName is empty.")]
    [SerializeField] private GameObject nextButton;

    private bool isMatchFinished;
    private bool isPaused;

    // INPUT:  panel refs, LevelManager singleton
    // OUTPUT: all panels closed, win/loss events subscribed, Next hidden if last level
    // USE:    Unity
    private void Start()
    {
        SetActiveSafe(pausePanel, false);
        SetActiveSafe(gameOverPage, false);
        SetActiveSafe(victoryPage, false);
        SetActiveSafe(pauseButton, true);

        if (nextButton != null) nextButton.SetActive(!string.IsNullOrEmpty(nextSceneName));

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelWon += TriggerVictory;
            LevelManager.Instance.OnLevelLost += TriggerGameOver;
        }
        else Debug.LogWarning("[MenuController] No LevelManager in scene!");
    }

    // INPUT:  none
    // OUTPUT: events unsubscribed
    // USE:    Unity
    private void OnDestroy()
    {
        if (LevelManager.Instance == null) return;
        LevelManager.Instance.OnLevelWon -= TriggerVictory;
        LevelManager.Instance.OnLevelLost -= TriggerGameOver;
    }

    // INPUT:  Esc key
    // OUTPUT: routes to TogglePause
    // USE:    Unity
    private void Update()
    {
        if (isMatchFinished) return;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            TogglePause();
    }

    // INPUT:  none
    // OUTPUT: pause <-> resume flip
    // USE:    Esc key AND the on-screen pause button's onClick — one entry point for both
    public void TogglePause()
    {
        if (isMatchFinished) return;
        if (isPaused) Resume();
        else Pause();
    }

    // INPUT:  none
    // OUTPUT: pause panel up, time frozen
    // USE:    TogglePause
    public void Pause()
    {
        if (isMatchFinished || isPaused) return;
        isPaused = true;
        SetActiveSafe(pausePanel, true);
        Time.timeScale = 0f;
    }

    // INPUT:  none
    // OUTPUT: pause panel down, time running
    // USE:    TogglePause, Resume button onClick
    public void Resume()
    {
        if (isMatchFinished || !isPaused) return;
        isPaused = false;
        SetActiveSafe(pausePanel, false);
        Time.timeScale = 1f;
    }

    // INPUT:  none (LevelManager.OnLevelLost)
    // OUTPUT: defeat panel only, time frozen
    // USE:    event
    public void TriggerGameOver()
    {
        if (isMatchFinished) return;
        EndMatch(gameOverPage);
    }

    // INPUT:  win condition type (unused — kept to match the event signature)
    // OUTPUT: victory panel only, time frozen
    // USE:    LevelManager.OnLevelWon event
    public void TriggerVictory(WinConditionType _)
    {
        if (isMatchFinished) return;
        EndMatch(victoryPage);
    }

    // INPUT:  the one panel to show
    // OUTPUT: latches match end, closes everything else, freezes time
    // USE:    TriggerGameOver, TriggerVictory — single funnel so end-states can't overlap
    private void EndMatch(GameObject panel)
    {
        isMatchFinished = true;
        isPaused = false;
        SetActiveSafe(pausePanel, false);
        SetActiveSafe(pauseButton, false);
        SetActiveSafe(panel, true);
        Time.timeScale = 0f;
    }

    // INPUT:  none
    // OUTPUT: main menu scene
    // USE:    Home button onClick (all three panels)
    public void Home()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    // INPUT:  none
    // OUTPUT: current scene reloaded
    // USE:    Restart button onClick (all three panels)
    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // INPUT:  nextSceneName (inspector)
    // OUTPUT: next level scene
    // USE:    Next button onClick (victory page). No-op if name empty.
    public void NextLevel()
    {
        if (string.IsNullOrEmpty(nextSceneName)) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextSceneName);
    }

    // INPUT:  object + desired state
    // OUTPUT: SetActive with null guard
    // USE:    everywhere above — panels are optional refs
    private static void SetActiveSafe(GameObject go, bool active) { if (go != null) go.SetActive(active); }
}