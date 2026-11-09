using EngineIndependentSaveSystem.Models;

namespace EngineIndependentSaveSystem.Services;

/// <summary>Validates game-specific consistency rules for V1 save data.</summary>
public static class GameSaveDataValidator
{
    public static void Validate(GameSaveData gameSaveData)
    {
        if (gameSaveData is null) throw new InvalidDataException("Save data cannot be null.");
        if (gameSaveData.SaveVersion != GameSaveData.CurrentSaveVersion)
        {
            throw new InvalidDataException($"Save version {gameSaveData.SaveVersion} is not supported.");
        }

        if (gameSaveData.Player is null) throw new InvalidDataException("Save data must contain player data.");
        if (gameSaveData.Enemies is null) throw new InvalidDataException("Save data must contain an enemy collection.");
        ValidatePosition(gameSaveData.Player.PositionDimension, gameSaveData.Player.Position2D, gameSaveData.Player.Position3D, "Player");

        var enemyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var enemy in gameSaveData.Enemies)
        {
            if (enemy is null) throw new InvalidDataException("Enemy data cannot be null.");
            if (string.IsNullOrWhiteSpace(enemy.Id)) throw new InvalidDataException("Enemy IDs must be non-empty.");
            if (!enemyIds.Add(enemy.Id)) throw new InvalidDataException($"Enemy ID '{enemy.Id}' appears more than once.");
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
        if (position2D is not null && (!float.IsFinite(position2D.X) || !float.IsFinite(position2D.Y)))
        {
            throw new InvalidDataException($"{owner} has non-finite 2D coordinates.");
        }

        if (position3D is not null && (!float.IsFinite(position3D.X) || !float.IsFinite(position3D.Y) || !float.IsFinite(position3D.Z)))
        {
            throw new InvalidDataException($"{owner} has non-finite 3D coordinates.");
        }
    }
}
