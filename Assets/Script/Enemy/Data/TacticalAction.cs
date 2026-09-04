using UnityEngine;

public abstract class TacticalAction : ScriptableObject
{
    // Returns how useful this action is right now, 0 = useless.
    // TacticalBrain multiplies this by CombatProfile weights and picks the max.
    // Swap this body for network inference later — signature stays identical.
    public abstract float ScoreAction(/* BattleState state */);
    public abstract void Execute(/* EnemyController owner */);
}