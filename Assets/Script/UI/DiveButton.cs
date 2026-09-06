using UnityEngine;
using UnityEngine.UI;

public class DiveButton : MonoBehaviour
{
    // INPUT:  playerDive — drag the Player here; alphas tune the translucent look
    // OUTPUT: click calls PlayerDive.ToggleDive(); icon dims when diving is blocked
    // USE:    attach to a UI Button on the HUD Canvas (icon image, no text)

    [SerializeField] private PlayerDive playerDive;
    [SerializeField, Range(0f, 1f)] private float readyAlpha = 0.6f;    // translucent but visible
    [SerializeField, Range(0f, 1f)] private float blockedAlpha = 0.2f;  // barely there = unavailable

    private Image icon;

    // INPUT:  none
    // OUTPUT: caches Image, hooks the Button's click to OnPressed
    // USE:    Unity
    private void Awake()
    {
        icon = GetComponent<Image>();
        GetComponent<Button>().onClick.AddListener(OnPressed);
    }

    // INPUT:  none
    // OUTPUT: forwards to PlayerDive
    // USE:    Button onClick
    private void OnPressed()
    {
        if (playerDive != null) playerDive.ToggleDive();
    }

    // INPUT:  PlayerDive.CanToggleDive
    // OUTPUT: icon alpha reflects availability every frame
    // USE:    Unity
    private void Update()
    {
        if (playerDive == null || icon == null) return;
        Color c = icon.color;
        c.a = playerDive.CanToggleDive ? readyAlpha : blockedAlpha;
        icon.color = c;
    }
}