using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/EnemyData")]
public class EnemyData : ScriptableObject
{
    [Header("Enemy Type")]
    public EnemyType enemyType = EnemyType.Mob;

    [Header("Health")]
    public int maxHp = 3;

    [Header("Movement")]
    public float moveSpeed = 3f;
    public float patrolSpeed = 1.5f;
    public float fleeSpeed = 4f;
    [Tooltip("Requires accel-based EnemyMovement — NOT yet implemented (current MoveToward sets velocity instantly).")]
    public float acceleration = 20f;
    public float arriveRadius = 0.3f;

    [Header("Vision")]
    public float fovAngle = 90f;
    public float viewDistance = 8f;
    public float loadDistance = 15f;
    public float awarenessDecayRate = 0.5f;

    [Header("Aiming")]
    public float turnRate = 180f;

    [Header("Combat")]
    public float attackRange = 6f;
    public float attackCooldown = 1f;
    public float windUpDuration = 0.3f;

    [Header("Mind")]
    [Tooltip("Personality + available tactics. Weights consumed by TacticalBrain (not yet live).")]
    public CombatProfile combatProfile;
}