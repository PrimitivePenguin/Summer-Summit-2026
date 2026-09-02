using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;


public class LevelController : MonoBehaviour
{
    public Button[] buttons;
    public GameObject levelButtons;

    private void Awake()
    {
        ButtonsToArray();
        int UnlockedLevel = PlayerPrefs.GetInt("UnlockedLevel", 1);
        for (int i = 0; i < buttons.Length; i++)
        {
            buttons[i].interactable = false;
        }
        for (int i = 0; i < UnlockedLevel; i++)
        {
            buttons[i].interactable = true;
        }
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void OpenLevel(int LevelId)
    {
        string LevelName = "L" + LevelId;
        // switch to async if loading takes too long -> need to add a loading screen
        // SceneManager.LoadSceneAsync(levelName);
        SceneManager.LoadScene(LevelName);
    }

    void ButtonsToArray()
    {
        int childCount = levelButtons.transform.childCount;
        buttons = new Button[childCount];
        for (int i = 0; i < childCount; i++)
        {
            buttons[i] = levelButtons.transform.GetChild(i).GetComponent<Button>();
        }
    }
    
    // // Add this to the end of whatever level we complete
    // void UnlockNewLevel()
    // {
    //     if (SceneManager.GetActiveScene().buildIndex >= PlayerPrefs.GetInt("ReachedIndex"))
    //     {
    //         PlayerPrefs.SetInt("ReachedIndex", SceneManager.GetActiveScene().buildIndex + 1);
    //         PlayerPrefs.SetInt("UnlockedLevel", SceneManager.GetInt("UnlockedLevel", 1) + 1);
    //         PlayerPrefs.Save();
    //     }
    // }
}
