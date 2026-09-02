using UnityEngine;
using UnityEngine.InputSystem;

public class MenuController : MonoBehaviour
{
    public GameObject menuCanvas; // Reference to the menu panel GameObject\
    public GameObject PauseButton; // Reference to the pause button GameObject
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        menuCanvas.SetActive(false); // Hide the menu panel at the start
    }

    // Update is called once per frame
    void Update()
    {
        // pause if esc pressed or pause button pressed
        if (Keyboard.current.escapeKey.wasPressedThisFrame) // Check if the Escape key is pressed
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
        menuCanvas.SetActive(true);
        PauseButton.SetActive(false);
        Time.timeScale = 0f;
    }

    public void Resume()
    {
        menuCanvas.SetActive(false);
        PauseButton.SetActive(true);
        Time.timeScale = 1f;
    }
    public void Home()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    public void Restart()
    {
        Time.timeScale = 1f;
        Debug.Log("Restarting level not implemented yet");
        // UnityEngine.SceneManagement.SceneManager.LoadScene(
        //     UnityEngine.SceneManagement.SceneManager.GetActiveScene().name
        // );
    }
}
