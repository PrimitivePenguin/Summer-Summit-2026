using UnityEngine;

public class SceneMusic : MonoBehaviour
{
    // INPUT:  track set in inspector (this scene's ambient/battle/menu music)
    // OUTPUT: MusicManager plays it on scene start (no restart if already playing)
    // USE:    one per scene, on any always-active object (GameSystem, LevelManager, an empty)

    [SerializeField] private MusicTrack track;

    // INPUT:  none
    // OUTPUT: track requested
    // USE:    Unity
    private void Start() => MusicManager.Instance.Play(track);
}