using System.Collections.Concurrent;
using System.Text;
using EngineIndependentSaveSystem.Abstractions;

namespace EngineIndependentSaveSystem.Storage;

/// <summary>Stores one serialized save in a file.</summary>
public sealed class FileSaveStorage : ISaveStorage
{
    public const long DefaultMaximumSaveFileSizeBytes = 10 * 1024 * 1024;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks = new(
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    private readonly string saveFilePath;
    private readonly long maximumSaveFileSizeBytes;
    private readonly SemaphoreSlim operationLock;

    /// <param name="saveFilePath">Trusted application path for the save file.</param>
    /// <param name="maximumSaveFileSizeBytes">Maximum number of bytes allowed when loading a save.</param>
    public FileSaveStorage(string saveFilePath, long maximumSaveFileSizeBytes = DefaultMaximumSaveFileSizeBytes)
    {
        if (string.IsNullOrWhiteSpace(saveFilePath)) throw new ArgumentException("A save file path is required.", nameof(saveFilePath));
        if (maximumSaveFileSizeBytes <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSaveFileSizeBytes), "The maximum save-file size must be positive.");

        this.saveFilePath = Path.GetFullPath(saveFilePath);
        this.maximumSaveFileSizeBytes = maximumSaveFileSizeBytes;
        operationLock = PathLocks.GetOrAdd(this.saveFilePath, static _ => new SemaphoreSlim(1, 1));
    }

    public async Task SaveAsync(string data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        await ExecuteLockedAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directory = Path.GetDirectoryName(saveFilePath) ?? throw new InvalidOperationException("The save file must have a parent directory.");
            Directory.CreateDirectory(directory);
            var temporaryFilePath = Path.Combine(directory, $".{Path.GetFileName(saveFilePath)}.{Guid.NewGuid():N}.tmp");
            Exception? operationFailure = null;

            try
            {
                await using (var stream = new FileStream(temporaryFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                await using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                {
                    await writer.WriteAsync(data.AsMemory(), cancellationToken);
                    await writer.FlushAsync(cancellationToken);
                    await stream.FlushAsync(cancellationToken);
                }

                // The successful check below is the cancellation commit point.
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(saveFilePath)) File.Replace(temporaryFilePath, saveFilePath, destinationBackupFileName: null);
                else File.Move(temporaryFilePath, saveFilePath);
            }
            catch (Exception exception)
            {
                operationFailure = exception;
                throw;
            }
            finally
            {
                try
                {
                    File.Delete(temporaryFilePath);
                }
                catch (Exception) when (operationFailure is not null)
                {
                    // Never replace the original write or replacement failure with cleanup noise.
                }
            }

            return true;
        }, cancellationToken);
    }

    public Task<string?> LoadAsync(CancellationToken cancellationToken = default) =>
        ExecuteLockedAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(saveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
                if (stream.Length > maximumSaveFileSizeBytes)
                {
                    throw new InvalidDataException($"The save file exceeds the configured limit of {maximumSaveFileSizeBytes} bytes.");
                }

                return await ReadWithSizeLimitAsync(stream, cancellationToken);
            }
            catch (FileNotFoundException)
            {
                return null;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }
        }, cancellationToken);

    public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) =>
        ExecuteLockedAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var stream = new FileStream(saveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                return Task.FromResult(true);
            }
            catch (FileNotFoundException)
            {
                return Task.FromResult(false);
            }
            catch (DirectoryNotFoundException)
            {
                return Task.FromResult(false);
            }
        }, cancellationToken);

    public async Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteLockedAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            File.Delete(saveFilePath);
            return Task.FromResult(true);
        }, cancellationToken);
    }

    // helper method to ensure that only one operation is performed at a time for a given save file path.
    private async Task<T> ExecuteLockedAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            return await operation();
        }
        finally
        {
            operationLock.Release();
        }
    }

    // Enforces the configured file-size limit while reading.
    // Reads the file in chunks to protect against files growing after the initial size check.
    private async Task<string> ReadWithSizeLimitAsync(FileStream stream, CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[4096];
        using var memoryStream = new MemoryStream();

        long totalBytesRead = 0;
        int byteRead;

        while ((byteRead = await stream.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (byteRead > maximumSaveFileSizeBytes - totalBytesRead)
            {
                throw new InvalidDataException($"The save file exceeds the configured limit of {maximumSaveFileSizeBytes} bytes.");
            }
            else
            {
                totalBytesRead += byteRead;
            }
            memoryStream.Write(buffer, 0, byteRead);
        }
        memoryStream.Position = 0;
        using var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }
}
