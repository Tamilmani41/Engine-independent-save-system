using System.Text;
using EngineIndependentSaveSystem.Abstractions;

namespace EngineIndependentSaveSystem.Storage;

/// <summary>Stores one serialized save in a file.</summary>
public sealed class FileSaveStorage : ISaveStorage
{
    private readonly string saveFilePath;

    public FileSaveStorage(string saveFilePath)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath)) throw new ArgumentException("A save file path is required.", nameof(saveFilePath));
        this.saveFilePath = Path.GetFullPath(saveFilePath);
    }

    public async Task SaveAsync(string data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        cancellationToken.ThrowIfCancellationRequested();
        var directory = Path.GetDirectoryName(saveFilePath) ?? throw new InvalidOperationException("The save file must have a parent directory.");
        Directory.CreateDirectory(directory);
        var temporaryFilePath = Path.Combine(directory, $".{Path.GetFileName(saveFilePath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
            await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(data.AsMemory(), cancellationToken);
                await writer.FlushAsync(cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(saveFilePath)) File.Replace(temporaryFilePath, saveFilePath, destinationBackupFileName: null);
            else File.Move(temporaryFilePath, saveFilePath);
        }
        finally
        {
            if (File.Exists(temporaryFilePath))
            {
                try { File.Delete(temporaryFilePath); }
                catch (IOException) { /* Preserve the original write or replacement error. */ }
            }
        }
    }

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return !File.Exists(saveFilePath) ? null : await File.ReadAllTextAsync(saveFilePath, cancellationToken);
    }

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(saveFilePath));
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(saveFilePath);
        return Task.CompletedTask;
    }
}
