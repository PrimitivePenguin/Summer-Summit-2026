using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;

public class BossCutscene : MonoBehaviour
{
    // INPUT:  all refs assigned in inspector; arenaTrigger fires this on lock
    // OUTPUT: freeze → pan → typewrite → unfreeze → boss music → arena engage
    // USE:    L3 only. Attach to GameSystem or any always-active object.

    [Header("Scene Refs")]
    [SerializeField] private BossArenaTrigger arenaTrigger;
    [SerializeField] private BossMusicCue bossMusicCue;

    [Header("Cutscene Canvas")]
    [SerializeField] private GameObject cutsceneCanvas;
    [SerializeField] private RectTransform nunImage;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private GameObject skipHint;

    [Header("Dialogue")]
    [Tooltip("Each string is one line, displayed in sequence.")]
    [SerializeField] private string[] lines = {
        "You dare enter my sanctum?",
        "Then you will serve as an example."
    };
    [SerializeField] private float typeSpeed = 28f;
    [SerializeField] private float linePause = 0.8f;

    [Header("Parallax")]
    [SerializeField] private float panAmount = 40f;
    [SerializeField] private float panDuration = 4f;

    private bool skipped;
    private bool running;

    // INPUT:  arenaTrigger ref
    // OUTPUT: subscribed to OnLocked
    // USE:    Unity
    private void Start()
    {
        if (arenaTrigger != null) arenaTrigger.OnLocked += OnArenaLocked;
    }

    // INPUT:  none
    // OUTPUT: unsubscribed
    // USE:    Unity
    private void OnDestroy()
    {
        if (arenaTrigger != null) arenaTrigger.OnLocked -= OnArenaLocked;
    }

    // INPUT:  F5 key — debug only, remove before shipping
    // OUTPUT: fires cutscene manually without entering the arena
    // USE:    testing
    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f5Key.wasPressedThisFrame)
            OnArenaLocked();
    }

    // INPUT:  none (called by BossArenaTrigger.OnLocked)
    // OUTPUT: cutscene coroutine started
    // USE:    event + F5 debug
    private void OnArenaLocked()
    {
        if (running) return;
        running = true;
        StartCoroutine(RunCutscene());
    }

    // INPUT:  none
    // OUTPUT: full cutscene sequence — freeze, pan, typewrite, skip gate, teardown
    // USE:    OnArenaLocked
    private IEnumerator RunCutscene()
    {
        // ── Freeze ──────────────────────────────────────────────────────────
        Time.timeScale = 0f;
        skipped = false;

        cutsceneCanvas.SetActive(true);
        if (dialogueText != null) dialogueText.text = "";
        if (nunImage != null) nunImage.anchoredPosition = new Vector2(-panAmount * 0.5f, 0f);

        // ── Pan + typewrite (all unscaled — timeScale is 0) ─────────────────
        float elapsed = 0f;
        int lineIndex = 0;
        string currentLine = "";
        float charTimer = 0f;

        while (!skipped && elapsed < panDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            // Parallax pan
            if (nunImage != null)
            {
                float t = Mathf.Clamp01(elapsed / panDuration);
                float x = Mathf.Lerp(-panAmount * 0.5f, panAmount * 0.5f, t);
                nunImage.anchoredPosition = new Vector2(x, nunImage.anchoredPosition.y);
            }

            // Typewriter
            if (lineIndex < lines.Length)
            {
                if (currentLine == "")
                {
                    currentLine = lines[lineIndex];
                    charTimer = 0f;
                }

                charTimer += Time.unscaledDeltaTime;
                int charsToShow = Mathf.Min(
                    Mathf.FloorToInt(charTimer * typeSpeed),
                    currentLine.Length);

                if (dialogueText != null)
                    dialogueText.text = currentLine.Substring(0, charsToShow);

                if (charsToShow >= currentLine.Length)
                {
                    yield return new WaitForSecondsRealtime(linePause);
                    lineIndex++;
                    currentLine = "";
                    if (dialogueText != null) dialogueText.text = "";
                }
            }

            // Skip input
            if (AnyKeyDown()) skipped = true;

            yield return null;
        }

        // Show last line instantly on skip
        if (skipped && dialogueText != null && lines.Length > 0)
            dialogueText.text = lines[lines.Length - 1];

        yield return new WaitForSecondsRealtime(skipped ? 0.1f : 0.3f);

        // ── Tear down ────────────────────────────────────────────────────────
        cutsceneCanvas.SetActive(false);
        Time.timeScale = 1f;

        // Phase 2 — doors slam, boss wakes, camera confines
        if (arenaTrigger != null) arenaTrigger.Engage();

        // Boss music after your configured delay
        if (bossMusicCue != null) bossMusicCue.Trigger();

        running = false;
    }

    // INPUT:  none
    // OUTPUT: true if any key or mouse button pressed this unscaled frame
    // USE:    skip gate inside RunCutscene
    private bool AnyKeyDown()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        return (kb != null && kb.anyKey.wasPressedThisFrame) ||
               (mouse != null && mouse.leftButton.wasPressedThisFrame);
    }
}