using UnityEngine;

[CreateAssetMenu(menuName = "ScriptableObjects/CombatProfile")]
public class CombatProfile : ScriptableObject
{
    [Tooltip("Descriptive tag; also drives visual (color) and available tactics.")]
    // public Archetype archetype = Archetype.Aggressive; do this later

    [Header("Personality weights (consumed by TacticalBrain)")]
    [Range(0f, 1f)] public float aggression;
    [Range(0f, 1f)] public float selfPreservation;
    [Range(0f, 1f)] public float territoriality;
    [Range(0f, 1f)] public float recklessness;
    [Range(0f, 1f)] public float groupInstinct;

    [Header("Available actions for this archetype")]
    public TacticalAction[] tactics;   // Elite/Leader simply have more entries
}