using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Health")]
    public int maxHp = 3;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float patrolSpeed = 1.5f;

    [Header("Vision")]
    public float fovAngle = 90f;
    public float viewDistance = 8f;       // active detection range
    public float loadDistance = 15f;      // beyond this, fully idle
    public float awarenessDecayRate = 0.5f; // how fast awareness drops after losing sight

    [Header("Aiming")] // Assume isTurnRate is on
    public float turnRate = 180f;         // degrees per second

    [Header("Combat")]
    public float attackRange = 6f;
    public float attackCooldown = 1f;
    public float windUpDuration = 0.3f;   // telegraph before firing
}