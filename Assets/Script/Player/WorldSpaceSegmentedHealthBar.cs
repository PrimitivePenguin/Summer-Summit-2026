using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class WorldSpaceSegmentedHealthBar : MonoBehaviour
{
    [Header("Segment Setup")]
    [SerializeField] private GameObject pipPrefab;
    [SerializeField] private Transform pipContainer;

    [Header("Position Lock")]
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 1.2f, 0f);

    private Damageable damageable;
    private Transform playerTransform;
    private readonly List<Image> pips = new List<Image>();
    private int currentPips;

    void Awake()
    {
        if (pipContainer == null) pipContainer = transform;

        damageable = GetComponentInParent<Damageable>();
        if (damageable != null)
        {
            playerTransform = damageable.transform;
        }
    }

    void Start()
    {
        if (damageable != null)
        {
            damageable.OnDamaged += HandleDamageTaken;
            InitializePips(damageable.maxHp);
        }
    }

    void LateUpdate()
    {
        if (playerTransform == null) return;

        // Locks rotation so the bar does not spin when the player aims
        transform.rotation = Quaternion.identity;
        transform.position = playerTransform.position + localOffset;
    }

    void OnDestroy()
    {
        if (damageable != null)
        {
            damageable.OnDamaged -= HandleDamageTaken;
        }
    }

    private void InitializePips(int maxHp)
    {
        foreach (Transform child in pipContainer)
        {
            Destroy(child.gameObject);
        }
        pips.Clear();

        currentPips = maxHp;

        for (int i = 0; i < maxHp; i++)
        {
            GameObject newPip = Instantiate(pipPrefab, pipContainer);
            pips.Add(newPip.GetComponent<Image>());
        }
    }

    private void HandleDamageTaken(int amount)
    {
        currentPips = Mathf.Max(0, currentPips - amount);

        for (int i = 0; i < pips.Count; i++)
        {
            pips[i].enabled = (i < currentPips);
        }
    }
}