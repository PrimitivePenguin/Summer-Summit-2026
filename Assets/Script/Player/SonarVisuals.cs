using System.Collections.Generic;
using UnityEngine;

public class SonarVisual : MonoBehaviour
{
    // INPUT:  radii/colors in inspector; Begin/End called by PlayerDive
    // OUTPUT: radial darkness mask following the player + fading green sonar dots on enemies
    // USE:    attach to Player root, drag into PlayerDive.sonar

    [Header("Darkness Mask (walls visible near you, black far away)")]
    [SerializeField] private float clearRadius = 2f;       // fully visible inside this
    [SerializeField] private float fadeEndRadius = 6f;     // fully black beyond this
    [SerializeField] private float maskExtent = 40f;       // half-size of the mask sprite; must exceed half the screen diagonal
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int maskSortingOrder = 65;

    [Header("Sonar Ping (green dots on enemies)")]
    [SerializeField] private float pingRange = 10f;
    [SerializeField] private float pingInterval = 1.5f;
    [SerializeField] private float dotFadeDuration = 1.2f;
    [SerializeField] private float dotSize = 0.35f;
    [SerializeField] private Color dotColor = new Color(0.2f, 1f, 0.3f, 1f);
    [SerializeField] private LayerMask enemyMask;
    [SerializeField] private int dotSortingOrder = 70;

    private SpriteRenderer mask;
    private Sprite dotSprite;
    private float pingTimer;
    private bool active;

    private class Dot { public SpriteRenderer sr; public float t; }
    private readonly List<Dot> dots = new List<Dot>();

    // INPUT:  none
    // OUTPUT: mask + dot sprites generated, mask object created (root-level, hidden)
    // USE:    Unity
    private void Awake()
    {
        mask = new GameObject("DarknessMask").AddComponent<SpriteRenderer>();
        mask.sprite = MakeRadialMask(1024);
        mask.sortingLayerName = sortingLayerName;
        mask.sortingOrder = maskSortingOrder;
        mask.transform.localScale = Vector3.one * (maskExtent * 2f);   // sprite is 1 unit wide
        mask.enabled = false;

        dotSprite = MakeCircle(64);
    }

    // INPUT:  none
    // OUTPUT: mask follows player; pings on interval; dots fade and die
    // USE:    Unity
    private void LateUpdate()
    {
        if (mask != null) mask.transform.position = transform.position;

        if (active)
        {
            pingTimer -= Time.deltaTime;
            if (pingTimer <= 0f) { Ping(); pingTimer = pingInterval; }
        }

        for (int i = dots.Count - 1; i >= 0; i--)
        {
            Dot d = dots[i];
            d.t -= Time.deltaTime;
            if (d.t <= 0f || d.sr == null)
            {
                if (d.sr != null) Destroy(d.sr.gameObject);
                dots.RemoveAt(i);
                continue;
            }
            Color c = dotColor; c.a = d.t / dotFadeDuration;
            d.sr.color = c;
        }
    }

    // INPUT:  none
    // OUTPUT: mask shown, first ping fires immediately
    // USE:    PlayerDive.Dive
    public void Begin()
    {
        active = true;
        pingTimer = 0f;
        if (mask != null) mask.enabled = true;
    }

    // INPUT:  none
    // OUTPUT: mask hidden, all dots cleared
    // USE:    PlayerDive.Surface
    public void End()
    {
        active = false;
        if (mask != null) mask.enabled = false;
        foreach (var d in dots) if (d.sr != null) Destroy(d.sr.gameObject);
        dots.Clear();
    }

    // INPUT:  enemies within pingRange (enemyMask)
    // OUTPUT: one snapshot dot per enemy at its current position — dots do NOT follow
    // USE:    LateUpdate on interval, Begin
    private void Ping()
    {
        foreach (var col in Physics2D.OverlapCircleAll(transform.position, pingRange, enemyMask))
        {
            var go = new GameObject("SonarDot");
            go.transform.position = col.bounds.center;
            go.transform.localScale = Vector3.one * dotSize;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = dotSprite;
            sr.color = dotColor;
            sr.sortingLayerName = sortingLayerName;
            sr.sortingOrder = dotSortingOrder;
            dots.Add(new Dot { sr = sr, t = dotFadeDuration });
        }
    }

    // INPUT:  texture size
    // OUTPUT: 1-unit sprite: transparent centre → opaque black, corners fully black
    // USE:    Awake
    private Sprite MakeRadialMask(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float half = size * 0.5f;
        float inner = clearRadius / maskExtent;      // as fraction of half-size
        float outer = fadeEndRadius / maskExtent;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                float a = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, outer, d));
                px[y * size + x] = new Color(0f, 0f, 0f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }

    // INPUT:  texture size
    // OUTPUT: 1-unit soft white circle sprite
    // USE:    Awake (tinted per dot by color)
    private Sprite MakeCircle(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float half = size * 0.5f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(half, half)) / half;
                float a = 1f - Mathf.SmoothStep(0.7f, 1f, d);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px); tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
    }
}