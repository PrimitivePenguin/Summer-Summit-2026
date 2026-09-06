using UnityEngine;

public class ExplosionFX : MonoBehaviour
{
    // INPUT:  duration, maxScale set in inspector on the prefab
    // OUTPUT: scales up and fades out over duration, then destroys self
    // USE:    attach to ExplosionFX prefab root; no other setup needed

    [SerializeField] private float duration = 0.25f;
    [SerializeField] private float maxScale = 3f;

    private float timer;
    private SpriteRenderer sr;

    // INPUT:  none
    // OUTPUT: cached SpriteRenderer, timer started
    // USE:    Unity
    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        timer = duration;
        transform.localScale = Vector3.zero;
    }

    // INPUT:  none
    // OUTPUT: lerps scale 0→maxScale and alpha 1→0, destroys when done
    // USE:    Unity
    private void Update()
    {
        timer -= Time.deltaTime;
        float t = 1f - Mathf.Clamp01(timer / duration);   // 0 at start, 1 at end

        transform.localScale = Vector3.one * Mathf.Lerp(0f, maxScale, t);

        if (sr != null)
        {
            Color c = sr.color;
            c.a = Mathf.Lerp(1f, 0f, t);
            sr.color = c;
        }

        if (timer <= 0f) Destroy(gameObject);
    }
}