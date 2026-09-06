using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WorldSpaceSegmentedHealthBar : MonoBehaviour
{
    [Header("Segment Setup")]
    [SerializeField] private GameObject pipPrefab;
    [SerializeField] private Transform pipContainer;
    [SerializeField] private int hpPerPip = 1;              // NEW — player 1, boss 4

    [Header("Target")]
    [SerializeField] private Damageable explicitTarget;      // NEW — drag Player or Boss here directly

    [Header("Position Lock (leave off for a fixed HUD bar)")]
    [SerializeField] private bool followOwner = false;       // NEW — default false now
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.2f, 0f);

    private Damageable damageable;
    private Transform ownerTransform;
    private readonly List<Image> pips = new List<Image>();
    private int currentHp;
    private int maxHp;

    void Awake()
    {
        if (pipContainer == null) pipContainer = transform;
        damageable = explicitTarget != null ? explicitTarget : GetComponentInParent<Damageable>();
        if (damageable != null) ownerTransform = damageable.transform;
    }

    void Start()
    {
        if (damageable == null) return;
        damageable.OnDamaged += HandleDamageTaken;
        maxHp = damageable.maxHp;
        currentHp = damageable.GetHp();
        InitializePips();
    }

    void LateUpdate()
    {
        if (!followOwner || ownerTransform == null) return;  // NEW — HUD bars skip this whole method
        transform.rotation = Quaternion.identity;
        transform.position = ownerTransform.position + localOffset;
    }

    void OnDestroy()
    {
        if (damageable != null) damageable.OnDamaged -= HandleDamageTaken;
    }

    private void InitializePips()
    {
        foreach (Transform child in pipContainer) Destroy(child.gameObject);
        pips.Clear();
        int pipCount = Mathf.CeilToInt(maxHp / (float)hpPerPip);   // CHANGED
        for (int i = 0; i < pipCount; i++)
            pips.Add(Instantiate(pipPrefab, pipContainer).GetComponent<Image>());
        Refresh();
    }

    private void HandleDamageTaken(int amount) { currentHp = Mathf.Max(0, currentHp - amount); Refresh(); }

    private void Refresh()   // CHANGED — was inline in HandleDamageTaken
    {
        int lit = Mathf.CeilToInt(currentHp / (float)hpPerPip);
        for (int i = 0; i < pips.Count; i++) pips[i].enabled = i < lit;
    }
}