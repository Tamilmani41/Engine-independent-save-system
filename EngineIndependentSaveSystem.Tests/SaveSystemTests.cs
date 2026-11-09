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
    public void JsonSerializer_RoundTrips_2DAnd3D_PlayerAndEnemies()
    {
        var serializer = new JsonSaveSerializer();
        var restored = serializer.Deserialize<GameSaveData>(serializer.Serialize(CreateValidGameSaveData()));

        Assert.Equal(GameSaveData.CurrentSaveVersion, restored.SaveVersion);
        Assert.Equal(new Position3D(1, 2, 3), restored.Player.Position3D);
        Assert.Equal(new Position2D(10, 20), restored.Enemies[0].Position2D);
        Assert.Equal(new Position3D(-1, 0, 8), restored.Enemies[1].Position3D);
    }

    [Theory]
    [InlineData("{ invalid json")]
    [InlineData("{\"SaveVersion\":1,\"Unexpected\":true}")]
    [InlineData("{\"Player\":{\"PositionDimension\":1,\"Position3D\":{\"X\":1,\"Y\":2,\"Z\":3},\"Health\":1,\"Score\":0},\"Enemies\":[]}")]
    [InlineData("{\"SaveVersion\":1,\"Player\":{\"Position3D\":{\"X\":1,\"Y\":2,\"Z\":3},\"Health\":1,\"Score\":0},\"Enemies\":[]}")]
    [InlineData("{\"SaveVersion\":1,\"Player\":{\"PositionDimension\":1,\"Position3D\":{\"X\":1,\"Y\":2,\"Z\":3},\"Score\":0},\"Enemies\":[]}")]
    public void JsonSerializer_RejectsMalformedUnknownOrMissingRequiredProperties(string json)
    {
        Assert.Throws<InvalidDataException>(() => new JsonSaveSerializer().Deserialize<GameSaveData>(json));
    }

    [Theory]
    [MemberData(nameof(InvalidGameSaveData))]
    public void SaveService_RejectsInvalidGameData(GameSaveData gameSaveData)
    {
        var service = new SaveService(new JsonSaveSerializer(), new UnusedStorage());

        Assert.Throws<InvalidDataException>(() => service.SaveAsync(gameSaveData).GetAwaiter().GetResult());
    }

    [Fact]
    public void SaveService_AcceptsUniqueEnemyIds()
    {
        GameSaveDataValidator.Validate(CreateValidGameSaveData());
    }

    [Fact]
    public async Task SaveService_SavesAndLoadsAFile()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var service = new SaveService(new JsonSaveSerializer(), new FileSaveStorage(Path.Combine(directory, "save.json")));
            await service.SaveAsync(CreateValidGameSaveData());
            var restored = await service.LoadAsync();

            Assert.True(await service.HasSaveAsync());
            Assert.NotNull(restored);
            Assert.Equal(75, restored.Player.Health);
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_MissingFileReturnsNullWithoutCreatingDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"save-system-{Guid.NewGuid():N}");
        try
        {
            var storage = new FileSaveStorage(Path.Combine(directory, "save.json"));
            Assert.Null(await storage.LoadAsync());
            Assert.False(await storage.ExistsAsync());
            Assert.False(Directory.Exists(directory));
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_DeletesExistingAndMissingFiles()
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
    public async Task FileStorage_CreatesParentDirectoriesWhenSaving()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "one", "two", "save.json");
            var storage = new FileSaveStorage(path);
            await storage.SaveAsync("content");
            Assert.Equal("content", await storage.LoadAsync());
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_EnforcesConfiguredMaximumLoadSize()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "save.json");
            var storage = new FileSaveStorage(path, maximumSaveFileSizeBytes: 5);
            await storage.SaveAsync("small");
            Assert.Equal("small", await storage.LoadAsync());

            await storage.SaveAsync("larger");
            await Assert.ThrowsAsync<InvalidDataException>(() => storage.LoadAsync());
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_SerializesConcurrentSavesForTheSamePath()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var path = Path.Combine(directory, "save.json");
            var first = new FileSaveStorage(path);
            var second = new FileSaveStorage(path);
            await Task.WhenAll(first.SaveAsync("first"), second.SaveAsync("second"));

            Assert.Contains(await first.LoadAsync(), new[] { "first", "second" });
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_CancellationBeforeCommitPreventsWriting()
    {
        var directory = CreateTemporaryDirectory();
        try
        {
            var storage = new FileSaveStorage(Path.Combine(directory, "save.json"));
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.SaveAsync("content", cancellation.Token));
            Assert.False(await storage.ExistsAsync());
        }
        finally { DeleteTemporaryDirectory(directory); }
    }

    [Fact]
    public async Task FileStorage_FailedReplacementPreservesPreviousSave_OnWindows()
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

    [Fact]
    public async Task FileStorage_EnforcesMaximumSizeInBytes()
    {
        var directory = CreateTemporaryDirectory();

        try
        {
            var path = Path.Combine(directory, "save.json");
            var storage = new FileSaveStorage(
                path,
                maximumSaveFileSizeBytes: 5);

            await storage.SaveAsync("ééé");

            await Assert.ThrowsAsync<InvalidDataException>(
                () => storage.LoadAsync());
        }
        finally
        {
            DeleteTemporaryDirectory(directory);
        }
    }

    public static IEnumerable<object[]> InvalidGameSaveData()
    {
        yield return [new GameSaveData { SaveVersion = 2, Player = ValidPlayer() }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = PositionDimension.TwoD, Position3D = new(1, 2, 3), Health = 1, Score = 0 } }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = PositionDimension.ThreeD, Position2D = new(1, 2), Health = 1, Score = 0 } }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = PositionDimension.TwoD, Position2D = new(1, 2), Position3D = new(1, 2, 3), Health = 1, Score = 0 } }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = PositionDimension.TwoD, Health = 1, Score = 0 } }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = (PositionDimension)5, Position3D = new(1, 2, 3), Health = 1, Score = 0 } }];
        yield return [new GameSaveData { Player = new PlayerSaveData { PositionDimension = PositionDimension.TwoD, Position2D = new(float.NaN, 2), Health = 1, Score = 0 } }];
        yield return [new GameSaveData { Player = ValidPlayer(), Enemies = [ValidEnemy("duplicate"), ValidEnemy("duplicate")] }];
    }

    private static GameSaveData CreateValidGameSaveData() => new()
    {
        SaveVersion = GameSaveData.CurrentSaveVersion,
        Player = ValidPlayer(),
        Enemies = [
            new EnemySaveData { Id = "slime-01", PositionDimension = PositionDimension.TwoD, Position2D = new(10, 20), IsAlive = true },
            new EnemySaveData { Id = "drone-02", PositionDimension = PositionDimension.ThreeD, Position3D = new(-1, 0, 8), IsAlive = false }
        ]
    };

    private static PlayerSaveData ValidPlayer() => new() { PositionDimension = PositionDimension.ThreeD, Position3D = new(1, 2, 3), Health = 75, Score = 450 };
    private static EnemySaveData ValidEnemy(string id) => new() { Id = id, PositionDimension = PositionDimension.TwoD, Position2D = new(1, 2), IsAlive = true };
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
