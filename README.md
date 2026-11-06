# Engine-independent save system

This .NET 10 library saves a small game state without Unity, Godot, Unreal, or engine vector types.

## Architecture

- `Models/` contains JSON-compatible game data.
- `Abstractions/` defines string serialization and storage contracts.
- `Serialization/JsonSaveSerializer` is a generic `System.Text.Json` serializer.
- `Storage/FileSaveStorage` handles one file and safe writes.
- `Services/SaveService` coordinates validation, serialization, and storage.

Every entity records `PositionDimension`. It has exactly one matching coordinate property: `Position2D` for `TwoD`, or `Position3D` for `ThreeD`. `SaveService` validates this rule before saving and after loading; the generic JSON serializer has no game-model rules.

## Usage

```csharp
var serializer = new JsonSaveSerializer();
var storage = new FileSaveStorage("saves/game.json");
var saves = new SaveService(serializer, storage);

await saves.SaveAsync(new GameSaveData
{
    Player = new PlayerSaveData
    {
        PositionDimension = PositionDimension.ThreeD,
        Position3D = new Position3D(12.5f, 1f, -4.25f),
        Health = 100,
        Score = 0
    }
});

GameSaveData? loaded = await saves.LoadAsync();
```

`LoadAsync` returns `null` only when no save exists. Malformed JSON and invalid game data throw meaningful exceptions.

## File-write behavior

Writes go to a uniquely named temporary file in the destination directory, are flushed and closed, then committed with `File.Replace` (when replacing an existing save) or `File.Move` (for a first save). Keeping both files in one directory gives the strongest replacement guarantees the filesystem supports. Atomicity varies by operating system and filesystem; `File.Replace` can also report a platform/filesystem error rather than silently degrading the operation. A failed temporary write does not touch the existing save.
