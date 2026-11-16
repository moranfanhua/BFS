using BlackFlowSpire.Map.Models;

namespace BlackFlowSpire.Map.Stages;

// Each version is a complete battle. Sources can be different native or mod
// encounters, including unrelated enemies, scenes and behavior.
public abstract class BlackFlowSpireStageVersionDefinition
{
    public abstract string Id { get; }
    public abstract string SourceEncounterId { get; }
    public abstract string TitleKey { get; }
    public abstract string DescriptionKey { get; }
    public virtual int Revision => 1;
    public virtual BlackFlowSpireBalanceProfile Balance => new();
    public virtual IReadOnlyList<BlackFlowSpireMonsterRule> MonsterRules => [];
    public virtual bool UseGenericEnemyLayout => false;

    // Called only when generating a new map. Use the supplied seed for any
    // additional choices; return a new list to change this version's enemies.
    public virtual IReadOnlyList<BlackFlowSpireEnemySlot> BuildFormation(
        IReadOnlyList<BlackFlowSpireEnemySlot> nativeFormation, ulong seed) => nativeFormation;

    public virtual void ValidateFormation(IReadOnlyList<BlackFlowSpireEnemySlot> enemies)
    {
        if (enemies.Count == 0 || enemies.Any(enemy => string.IsNullOrWhiteSpace(enemy.MonsterId)))
            throw new InvalidDataException($"Stage version {Id} has an invalid formation.");
    }

    public void Validate()
    {
        if (new[] { Id, SourceEncounterId, TitleKey, DescriptionKey }.Any(string.IsNullOrWhiteSpace) || Revision < 1)
            throw new InvalidDataException("Invalid stage version definition.");
        Balance.Validate();
        foreach (var rule in MonsterRules) rule.Validate();
    }
}

