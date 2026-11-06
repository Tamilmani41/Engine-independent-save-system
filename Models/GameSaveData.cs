namespace EngineIndependentSaveSystem.Models;

/// <summary>The complete V1 game state stored in a save file.</summary>
public sealed class GameSaveData
{
    public PlayerSaveData Player { get; init; } = new();
    public IReadOnlyList<EnemySaveData> Enemies { get; init; } = [];
}
