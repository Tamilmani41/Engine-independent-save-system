# Engine-independent save system

This .NET 10 library stores a small game state without Unity, Godot, Unreal, or engine vector types.

## Structure

- `Models/` defines JSON-compatible save data.
- `Abstractions/` defines serialization and storage contracts.
- `Serialization/JsonSaveSerializer` provides generic, formatted JSON.
- `Storage/FileSaveStorage` provides bounded, file-based storage.
- `Services/SaveService` coordinates validation, serialization, and storage.

## Save format and validation

`GameSaveData.SaveVersion` is required and must equal `1`. Future schema changes must increment the version; migrations are deliberately outside V1.

JSON is strict: unknown properties, missing required properties, malformed JSON, and unsupported constructor data are rejected. A saved player/enemy must declare `PositionDimension` and contain exactly one matching coordinate object: `Position2D` for `TwoD`, or `Position3D` for `ThreeD`. Coordinates must be finite, enemy IDs must be non-empty and unique using ordinal comparison, and unsupported versions are rejected. Health and score have no library-imposed domain range.

## Usage

```csharp
var saves = new SaveService(
    new JsonSaveSerializer(),
    new FileSaveStorage("saves/game.json"));

await saves.SaveAsync(new GameSaveData
{
    SaveVersion = GameSaveData.CurrentSaveVersion,
    Player = new PlayerSaveData
    {
        PositionDimension = PositionDimension.ThreeD,
        Position3D = new Position3D(12.5f, 1f, -4.25f),
        Health = 100,
        Score = 0
    }
});
```

## Storage behavior

`FileSaveStorage` accepts a trusted application path. It uses a 10 MB maximum load size by default; configure another positive byte limit with `new FileSaveStorage(path, maximumSaveFileSizeBytes)`. The limit is checked before the complete file is read into memory. Missing files return `null`; access and I/O failures propagate.

Writes use a uniquely named temporary file in the destination directory, flush and close it, then commit with `File.Replace` for replacement or `File.Move` for a first save. This preserves the previous save if temporary writing fails. A process crash can leave a temporary file; V1 intentionally does not perform stale-file recovery.

Operations through `FileSaveStorage` instances targeting the same normalized path are serialized within this process. Cross-process synchronization, symlink alias coordination, and crash recovery are not guaranteed. Replacement atomicity and `File.Replace` support depend on the operating system and filesystem; failures are propagated rather than silently weakened.

Cancellation is cooperative. It is honored before and during temporary-file writing and up to the check immediately before replacement—the commit point. Cancellation after that point does not roll back a committed save.

## Deliberately deferred from V1

Cloud storage, encryption, compression, backup slots, autosave, migrations, stale-temp recovery, cross-process locking, and game-engine adapters.
