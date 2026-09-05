using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject menuCanvas;    // Pause menu panel
    public GameObject gameOverPage;  // Game Over / Defeat panel
    public GameObject victoryPage;   // Victory panel
    public GameObject pauseButton;   // In-game pause button

    private bool isMatchFinished = false;

    void Start()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (gameOverPage != null) gameOverPage.SetActive(false);
        if (victoryPage != null) victoryPage.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);

        // Subscribe to LevelManager events
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelWon += TriggerVictory;
            LevelManager.Instance.OnLevelLost += TriggerGameOver;
        }
        else
        {
            Debug.LogWarning("[MenuController] No LevelManager found in scene!");
        }
    }

    void OnDestroy()
    {
        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.OnLevelWon -= TriggerVictory;
            LevelManager.Instance.OnLevelLost -= TriggerGameOver;
        }
    }

    void Update()
    {
        // Don't allow pausing once the match has ended
        if (isMatchFinished) return;

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            if (menuCanvas.activeSelf)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }
    }

    public void Pause()
    {
        if (isMatchFinished) return;

        menuCanvas.SetActive(true);
        if (pauseButton != null) pauseButton.SetActive(false);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (isMatchFinished) return;

        menuCanvas.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
        Time.timeScale = 1f;
    }

    public void TriggerGameOver()
    {
        if (isMatchFinished) return;
        isMatchFinished = true;

        CloseAllPanels();

        if (gameOverPage != null)
        {
            gameOverPage.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[MenuController] gameOverPage reference missing!");
        }

        Time.timeScale = 0f;
    }

    public void TriggerVictory(WinConditionType type)
    {
        if (isMatchFinished) return;
        isMatchFinished = true;

        CloseAllPanels();

        if (victoryPage != null)
        {
            victoryPage.SetActive(true);
        }
        else
        {
            Debug.LogWarning("[MenuController] victoryPage reference missing!");
        }

        Time.timeScale = 0f;
    }

    private void CloseAllPanels()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);
    }

    public void Home()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("MainMenu");
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    public void NextLevel(string nextSceneName)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(nextSceneName);
    }
}