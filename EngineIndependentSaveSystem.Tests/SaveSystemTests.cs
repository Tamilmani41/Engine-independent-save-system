using EngineIndependentSaveSystem.Abstractions;
using EngineIndependentSaveSystem.Models;
using EngineIndependentSaveSystem.Serialization;
using EngineIndependentSaveSystem.Services;
using EngineIndependentSaveSystem.Storage;
using Xunit;

namespace EngineIndependentSaveSystem.Tests;

public sealed class SaveSystemTests
{
    [Fact]
    public void JsonSerializer_RoundTrips_Player_And_MultipleEnemies_With_2D_And_3D_Positions()
    {
        var serializer = new JsonSaveSerializer();
        var original = CreateValidGameSaveData();

        var json = serializer.Serialize(original);
        var restored = serializer.Deserialize<GameSaveData>(json);

        Assert.Contains("\n", json);
        Assert.Equal(new Position3D(1, 2, 3), restored.Player.Position3D);
        Assert.Equal(2, restored.Enemies.Count);
        Assert.Equal(new Position2D(10, 20), restored.Enemies[0].Position2D);
        Assert.Equal(new Position3D(-1, 0, 8), restored.Enemies[1].Position3D);
    }

    [Fact]
    public void JsonSerializer_Rejects_InvalidJson()
    {
        var serializer = new JsonSaveSerializer();

        var exception = Assert.Throws<InvalidDataException>(() => serializer.Deserialize<GameSaveData>("{ invalid json"));

        Assert.IsType<System.Text.Json.JsonException>(exception.InnerException);
    }

    [Theory]
    [MemberData(nameof(InvalidPositionData))]
    public void SaveService_Rejects_InconsistentPositionData(GameSaveData gameSaveData)
    {
        var service = new SaveService(new JsonSaveSerializer(), new UnusedStorage());

        Assert.Throws<InvalidDataException>(() => service.SaveAsync(gameSaveData).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task SaveService_Saves_And_Loads_A_File()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var service = new SaveService(new JsonSaveSerializer(), new FileSaveStorage(Path.Combine(directory, "save.json")));
            var original = CreateValidGameSaveData();

            await service.SaveAsync(original);
            var restored = await service.LoadAsync();

            Assert.True(await service.HasSaveAsync());
            Assert.NotNull(restored);
            Assert.Equal(original.Player.Health, restored.Player.Health);
            Assert.Equal(original.Enemies[0].Id, restored.Enemies[0].Id);
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_LoadingMissingFile_ReturnsNull_WithoutCreatingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"save-system-{Guid.NewGuid():N}");
        var storage = new FileSaveStorage(Path.Combine(directory, "save.json"));
        try
        {
            Assert.Null(await storage.LoadAsync());
            Assert.False(Directory.Exists(directory));
            Assert.False(await storage.ExistsAsync());
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_Deletes_Existing_And_MissingFiles()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var storage = new FileSaveStorage(Path.Combine(directory, "save.json"));
            await storage.SaveAsync("previous");
            await storage.DeleteAsync();
            await storage.DeleteAsync();

            Assert.False(await storage.ExistsAsync());
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_Creates_MissingParentDirectories_WhenSaving()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "one", "two", "save.json");
            var storage = new FileSaveStorage(path);

            await storage.SaveAsync("content");

            Assert.True(File.Exists(path));
            Assert.Equal("content", await storage.LoadAsync());
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_Cancellation_IsPropagated()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var storage = new FileSaveStorage(Path.Combine(directory, "save.json"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync("content", cancellation.Token));
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_FailedReplacement_PreservesPreviousSave_OnWindows()
    {
        if (!OperatingSystem.IsWindows()) return;

        var directory = CreateTemporaryDirectory();
        var path = Path.Combine(directory, "save.json");
        try
        {
            var storage = new FileSaveStorage(path);
            await storage.SaveAsync("previous");
            using (File.Open(path, FileMode.Open, FileAccess.Read, FileShare.None))
            {
                await Assert.ThrowsAnyAsync<IOException>(() => storage.SaveAsync("replacement"));
            }

            Assert.Equal("previous", await storage.LoadAsync());
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    public static IEnumerable<object[]> InvalidPositionData()
    {
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = PositionDimension.TwoD, Position3D = new(1, 2, 3) } }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = PositionDimension.ThreeD, Position2D = new(1, 2) } }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = PositionDimension.TwoD, Position2D = new(1, 2), Position3D = new(1, 2, 3) } }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = PositionDimension.TwoD } }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = (PositionDimension)5, Position3D = new(1, 2, 3)}}];
    }

    private static GameSaveData CreateValidGameSaveData() => new()
    {
        Player = new PlayerSaveData { PositionDimension = PositionDimension.ThreeD, Position3D = new(1, 2, 3), Health = 75, Score = 450 },
        Enemies =
        [
            new EnemySaveData { Id = "slime-01", PositionDimension = PositionDimension.TwoD, Position2D = new(10, 20), IsAlive = true },
            new EnemySaveData { Id = "drone-02", PositionDimension = PositionDimension.ThreeD, Position3D = new(-1, 0, 8), IsAlive = false }
        ]
    };

    private static string CreateTemporaryDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"save-system-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        return directory;
    }

    private static void DeleteTemporaryDirectory(string directory)
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }

    private sealed class UnusedStorage : ISaveStorage
    {
        public Task SaveAsync(string data, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Storage should not be called for invalid data.");
        public Task<string?> LoadAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
