namespace EngineIndependentSaveSystem.Models;

/// <summary>Data persisted for one enemy.</summary>
public sealed class EnemySaveData
{
    /// <summary>A stable game-defined identifier for this enemy.</summary>
    public string Id { get; init; } = string.Empty;
    public PositionDimension PositionDimension { get; init; }
    public Position2D? Position2D { get; init; }
    public Position3D? Position3D { get; init; }
    public bool IsAlive { get; init; }
}
