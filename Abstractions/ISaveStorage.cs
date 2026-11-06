namespace EngineIndependentSaveSystem.Abstractions;

/// <summary>Persists an already-serialized save representation.</summary>
public interface ISaveStorage
{
    Task SaveAsync(string data, CancellationToken cancellationToken = default);
    Task<string?> LoadAsync(CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(CancellationToken cancellationToken = default);
    Task DeleteAsync(CancellationToken cancellationToken = default);
}
