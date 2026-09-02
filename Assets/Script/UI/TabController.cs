using UnityEngine;
using UnityEngine.UI;

public class TabController : MonoBehaviour
{
    public Image[] tabImages;
    public GameObject[] pages;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ActivateTab(0); // Activate the first tab by default
        // if I don't call this it goes to last opened by default
    }

    // Update is called once per frame
    public void ActivateTab(int tabIndex)
    {
        for (int i = 0; i < pages.Length; i++) 
        {
            pages[i].SetActive(false);
            tabImages[i].color = Color.gray; // Set inactive tab color 
        }
        pages[tabIndex].SetActive(true);
        tabImages[tabIndex].color = Color.white; // active tab color
    }
}
