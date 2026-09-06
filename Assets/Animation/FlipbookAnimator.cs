using UnityEngine;

// Cycles through a fixed array of sprites at a fixed rate, then optionally
// destroys itself. Drop on the same GameObject as the SpriteRenderer.
// USE: explosion / kaboom effect prefabs referenced by ArtilleryStrikeAbility.effectPrefab
//      and Bullet.Explode's aoeEffectPrefab (rocket).
[RequireComponent(typeof(SpriteRenderer))]
public class FlipbookAnimator : MonoBehaviour
{
    [SerializeField] private Sprite[] frames;
    [SerializeField] private float fps = 24f;
    [SerializeField] private bool destroyOnFinish = true;   // false = loops forever

    private SpriteRenderer sr;
    private int index;
    private float timer;

    private void Awake() => sr = GetComponent<SpriteRenderer>();

    private void OnEnable()
    {
        index = 0;
        timer = 0f;
        if (frames != null && frames.Length > 0) sr.sprite = frames[0];
    }

    private void Update()
    {
        if (frames == null || frames.Length == 0) return;

        timer += Time.deltaTime;
        float frameTime = 1f / fps;
        if (timer < frameTime) return;
        timer -= frameTime;

        index++;
        if (index >= frames.Length)
        {
            if (destroyOnFinish) { Destroy(gameObject); return; }
            index = 0;
        }
        sr.sprite = frames[index];
    }
}