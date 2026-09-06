using UnityEngine;

public class BossMusicCue : MonoBehaviour
{
    // INPUT:  bossTrack + delay; optional BossArenaTrigger to auto-fire on lock
    // OUTPUT: boss music starts `delayAfterTrigger` real seconds after Trigger()
    // USE:    L3 only. Later the cutscene calls Trigger() itself — set triggerOnArenaLock false then.

    [SerializeField] private MusicTrack bossTrack;
    [Tooltip("Real seconds between Trigger() and the first note. Tune to your voice line / cutscene.")]
    [SerializeField] private float delayAfterTrigger = 3f;

    [Header("Auto-hook (until the cutscene exists)")]
    [SerializeField] private bool triggerOnArenaLock = true;
    [SerializeField] private BossArenaTrigger arenaTrigger;

    // INPUT:  arenaTrigger ref
    // OUTPUT: subscribed to OnLocked if auto-hook enabled
    // USE:    Unity
    private void Start()
    {
        if (triggerOnArenaLock && arenaTrigger != null) arenaTrigger.OnLocked += Trigger;
    }

    // INPUT:  none
    // OUTPUT: unsubscribed
    // USE:    Unity
    private void OnDestroy()
    {
        if (arenaTrigger != null) arenaTrigger.OnLocked -= Trigger;
    }

    // INPUT:  none
    // OUTPUT: boss track scheduled after the delay
    // USE:    BossArenaTrigger.OnLocked now; BossCutscene later; also callable from a debug key
    public void Trigger() => MusicManager.Instance.Play(bossTrack, delayAfterTrigger);
}