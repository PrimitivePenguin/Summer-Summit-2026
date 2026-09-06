using UnityEngine;
using UnityEngine.UI;

public class DiveBar : MonoBehaviour
{
    // INPUT:  playerDive — drag the Player (its PlayerDive component) here in inspector
    // OUTPUT: Slider fill mirrors PlayerDive.AirRatio (0-1) every frame
    // USE:    attach to a Dive Slider on the HUD Canvas, next to the HP bar

    [SerializeField] private PlayerDive playerDive;
    private Slider slider;

    // INPUT:  none
    // OUTPUT: slider configured as a non-interactive 0-1 gauge
    // USE:    Unity
    private void Awake()
    {
        slider = GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.interactable = false;
    }

    // INPUT:  none (reads PlayerDive.AirRatio)
    // OUTPUT: slider.value updated
    // USE:    Unity. Polling on purpose — PlayerDive has no change event,
    //         and one float read per frame keeps PlayerDive completely untouched.
    private void Update()
    {
        if (playerDive != null) slider.value = playerDive.AirRatio;
    }
}