namespace EngineIndependentSaveSystem.Models;

/// <summary>Data persisted for the player.</summary>
public sealed class PlayerSaveData
{
    public PositionDimension PositionDimension { get; init; }
    public Position2D? Position2D { get; init; }
    public Position3D? Position3D { get; init; }
    public int Health { get; init; }
    public int Score { get; init; }
}
