using EngineIndependentSaveSystem.Abstractions;
using EngineIndependentSaveSystem.Models;

namespace EngineIndependentSaveSystem.Services;

/// <summary>Coordinates validation, serialization, and storage of game save data.</summary>
public sealed class SaveService
{
    private readonly ISaveSerializer serializer;
    private readonly ISaveStorage storage;

    public SaveService(ISaveSerializer serializer, ISaveStorage storage)
    {
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public async Task SaveAsync(GameSaveData gameSaveData, CancellationToken cancellationToken = default)
    {
        GameSaveDataValidator.Validate(gameSaveData);
        await storage.SaveAsync(serializer.Serialize(gameSaveData), cancellationToken);
    }

    public async Task<GameSaveData?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var serializedData = await storage.LoadAsync(cancellationToken);
        if (serializedData is null) return null;
        var gameSaveData = serializer.Deserialize<GameSaveData>(serializedData);
        GameSaveDataValidator.Validate(gameSaveData);
        return gameSaveData;
    }

    public Task<bool> HasSaveAsync(CancellationToken cancellationToken = default) => storage.ExistsAsync(cancellationToken);
    public Task DeleteSaveAsync(CancellationToken cancellationToken = default) => storage.DeleteAsync(cancellationToken);
}
