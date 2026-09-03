using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class MenuController : MonoBehaviour
{
    [Header("Panels")]
    public GameObject menuCanvas;    // Pause menu panel
    public GameObject gameOverPage;  // Game Over panel
    public GameObject pauseButton;   // In-game pause button

    [Header("References")]
    [SerializeField] private Damageable playerDamageable;

    private bool isGameOver = false;

    void Awake()
    {
        // Try to find the player's Damageable component if not assigned in Inspector
        if (playerDamageable == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerDamageable = player.GetComponent<Damageable>();
            }
        }

        if (playerDamageable != null)
        {
            playerDamageable.OnDeath += TriggerGameOver;
        }
    }

    void Start()
    {
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (gameOverPage != null) gameOverPage.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
    }

    void OnDestroy()
    {
        if (playerDamageable != null)
        {
            playerDamageable.OnDeath -= TriggerGameOver;
        }
    }

    void Update()
    {
        // Prevent opening the pause menu if the player is already dead
        if (isGameOver) return;

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
        if (isGameOver) return;

        menuCanvas.SetActive(true);
        if (pauseButton != null) pauseButton.SetActive(false);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        if (isGameOver) return;

        menuCanvas.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(true);
        Time.timeScale = 1f;
    }

    public void TriggerGameOver()
    {
        Debug.Log("TriggerGameOver CALLED!");
        isGameOver = true;

        // Close pause menu if it was somehow open, hide pause button
        if (menuCanvas != null) menuCanvas.SetActive(false);
        if (pauseButton != null) pauseButton.SetActive(false);

        // Show Game Over UI and freeze time
        if (gameOverPage != null){
            gameOverPage.SetActive(true);
        }
        else{
            Debug.Log("GameOverPage doesn't exist");
        }
        Time.timeScale = 0f;
    }

    public void Home()
    {
        Time.timeScale = 1f; // Critical: always restore timeScale before changing scenes!
        SceneManager.LoadScene("MainMenu");
    }

    public void Restart()
    {
        Time.timeScale = 1f; // Critical: restore timeScale so the restarted level isn't frozen
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}