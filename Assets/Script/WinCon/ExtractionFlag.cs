using System;
using UnityEngine;

public class ExtractionFlag : MonoBehaviour
{
    public event Action OnFlagReached;

    [Header("Extraction Settings")]
    [SerializeField] private float reachDistance = 1.0f;

    private Transform playerTransform;
    private bool reached = false;

    private void Start()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }

    private void Update()
    {
        if (reached || playerTransform == null) return;

        // Check squared distance to save computing a square root every frame
        Vector2 delta = (Vector2)transform.position - (Vector2)playerTransform.position;
        if (delta.sqrMagnitude <= reachDistance * reachDistance)
        {
            reached = true;
            OnFlagReached?.Invoke();
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, reachDistance);
    }
}