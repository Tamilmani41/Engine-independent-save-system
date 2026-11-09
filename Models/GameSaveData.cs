using System.Text.Json.Serialization;

namespace EngineIndependentSaveSystem.Models;

/// <summary>The complete V1 game state stored in a save file.</summary>
public sealed class GameSaveData
{
    public const int CurrentSaveVersion = 1;

    [JsonRequired]
    public int SaveVersion { get; init; } = CurrentSaveVersion;

    [JsonRequired]
    public PlayerSaveData Player { get; init; } = new();

    [JsonRequired]
    public IReadOnlyList<EnemySaveData> Enemies { get; init; } = [];
}
