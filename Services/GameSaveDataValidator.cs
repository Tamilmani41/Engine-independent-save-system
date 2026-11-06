using EngineIndependentSaveSystem.Models;

namespace EngineIndependentSaveSystem.Services;

/// <summary>Validates game-specific consistency rules for V1 save data.</summary>
public static class GameSaveDataValidator
{
    public static void Validate(GameSaveData gameSaveData)
    {
        ArgumentNullException.ThrowIfNull(gameSaveData);
        ArgumentNullException.ThrowIfNull(gameSaveData.Player);
        ArgumentNullException.ThrowIfNull(gameSaveData.Enemies);
        ValidatePosition(gameSaveData.Player.PositionDimension, gameSaveData.Player.Position2D, gameSaveData.Player.Position3D, "Player");
        foreach (var enemy in gameSaveData.Enemies)
        {
            ArgumentNullException.ThrowIfNull(enemy);
            if (string.IsNullOrWhiteSpace(enemy.Id)) throw new InvalidDataException("Enemy IDs must be non-empty.");
            ValidatePosition(enemy.PositionDimension, enemy.Position2D, enemy.Position3D, $"Enemy '{enemy.Id}'");
        }
    }

    private static void ValidatePosition(PositionDimension dimension, Position2D? position2D, Position3D? position3D, string owner)
    {
        if (!Enum.IsDefined(dimension)) throw new InvalidDataException($"{owner} has an unsupported position dimension {dimension} .");
        if (position2D is not null && position3D is not null) throw new InvalidDataException($"{owner} cannot contain both 2D and 3D positions.");
        if (position2D is null && position3D is null) throw new InvalidDataException($"{owner} must contain a position.");
        if (dimension == PositionDimension.TwoD && position2D is null) throw new InvalidDataException($"{owner} is marked as 2D but has no 2D position.");
        if (dimension == PositionDimension.ThreeD && position3D is null) throw new InvalidDataException($"{owner} is marked as 3D but has no 3D position.");
    }
}
