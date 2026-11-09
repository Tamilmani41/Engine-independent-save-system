# Engine-Independent Save System

A reusable C# save system designed to operate independently of game engines such as Unity, Godot, and Unreal Engine.

The project separates game data, serialization, storage, and service responsibilities through interfaces, making the system easier to maintain and extend.

## Features

- Engine-independent save data models.
- Support for both 2D and 3D positions.
- JSON serialization.
- Interface-based serialization and storage.
- File-based local storage.
- Save data validation.
- Configurable maximum save-file size.
- Asynchronous file operations with cancellation support.
- Temporary-file-based save replacement.
- Unit tests using xUnit.

## Architecture

The system separates responsibilities into distinct layers:

| Component | Responsibility |
|---|---|
| Models | Defines game save data and position representations. |
| Abstractions | Defines serialization and storage contracts. |
| Serialization | Converts save data to and from JSON. |
| Storage | Handles local file operations. |
| Services | Coordinates validation, serialization, and storage. |

### Project Structure

```text
EngineIndependentSaveSystem/
│
├── Models/
│   ├── GameSaveData.cs
│   ├── PlayerSaveData.cs
│   ├── EnemySaveData.cs
│   ├── Position2D.cs
│   ├── Position3D.cs
│   └── PositionDimension.cs
│
├── Abstractions/
│   ├── ISaveSerializer.cs
│   └── ISaveStorage.cs
│
├── Serialization/
│   └── JsonSaveSerializer.cs
│
├── Storage/
│   └── FileSaveStorage.cs
│
└── Services/
    ├── SaveService.cs
    └── GameSaveDataValidator.cs
```

### Dependency Design

`SaveService` depends on `ISaveSerializer` and `ISaveStorage`, rather than directly depending on concrete implementations.

This allows serialization and storage implementations to be replaced independently, provided they follow their respective interface contracts.

For example, the JSON serializer could eventually be replaced with another serialization implementation without requiring changes to the service's coordination logic.

Similarly, a different storage implementation could be introduced without changing the save-data models.

These alternative implementations are architectural extension points, not features included in V1.

## Save Format and Validation

The current save format uses JSON.

`GameSaveData.SaveVersion` is required and must equal `1`.

Future schema changes must increment the version. Automatic save migrations are outside the scope of V1.

The JSON serializer uses strict deserialization rules:

- Unknown properties are rejected.
- Missing required properties are rejected.
- Malformed JSON is rejected.
- Unsupported constructor data is rejected.

### Position Representation

Each player and enemy record must declare its position dimension.

| Position Dimension | Required Data |
|---|---|
| `TwoD` | `Position2D` |
| `ThreeD` | `Position3D` |

Exactly one matching coordinate object must be populated.

Additional validation includes:

- Coordinates must contain finite values.
- Enemy IDs must be non-empty.
- Enemy IDs must be unique using ordinal comparison.
- Unsupported save versions are rejected.
- Health and score have no library-imposed domain range.

## Usage

The following example demonstrates constructing the service and saving game data.

```csharp
var saveService = new SaveService(
    new JsonSaveSerializer(),
    new FileSaveStorage("saves/game.json"));

var saveData = new GameSaveData
{
    SaveVersion = GameSaveData.CurrentSaveVersion,

    Player = new PlayerSaveData
    {
        PositionDimension = PositionDimension.ThreeD,
        Position3D = new Position3D(12.5f, 1f, -4.25f),
        Health = 100,
        Score = 0
    }
};

// Save game data
await saveService.SaveAsync(saveData);

// Check whether a save exists
bool hasSave = await saveService.HasSaveAsync();

// Load game data
GameSaveData? loadedData = await saveService.LoadAsync();

// Delete the save
await saveService.DeleteAsync();
```

The example uses the default JSON serializer and local file storage implementations.

## Storage Behaviour

`FileSaveStorage` accepts a trusted application path.

### Maximum File Size

The default maximum load size is 10 MB.

A different positive byte limit can be configured:

```csharp
var storage = new FileSaveStorage(
    "saves/game.json",
    maximumSaveFileSizeBytes: 5 * 1024 * 1024);
```

The size limit is checked before the complete file is read into memory, including enforcement while reading the file in chunks.

### Missing Files and Errors

- Missing files return `null` when loading.
- Missing files are handled harmlessly during deletion.
- Access and other I/O failures propagate to the caller.

### Save Writing Behaviour

Save operations use a temporary file in the destination directory.

The process:

1. Creates a uniquely named temporary file.
2. Writes the save data.
3. Flushes and closes the file.
4. Replaces the existing save using `File.Replace`, or moves the temporary file into place using `File.Move` for a first save.

This approach helps preserve the previous save if temporary-file writing fails.

However, replacement behaviour depends on the operating system and filesystem. It is not a universal guarantee of atomicity or crash durability.

A process crash can leave a temporary file behind. V1 does not implement stale temporary-file recovery.

### Concurrency

Operations through `FileSaveStorage` instances targeting the same normalized path are serialized within the current process.

The implementation does not guarantee:

- Cross-process synchronization.
- Coordination between symlink aliases.
- Recovery after an unexpected process or system crash.

### Cancellation

File operations support cooperative cancellation.

Cancellation is checked before and during temporary-file writing and up to the check immediately before replacement.

Once the commit operation begins, cancellation does not roll back a committed save.

## Requirements

- .NET 10 SDK.
- A compatible development environment.

The core library does not require a game engine.

## Building and Testing

Clone the repository and navigate to the solution directory.

Restore dependencies:

```bash
dotnet restore EngineIndependentSaveSystem.slnx
```

Build the solution:

```bash
dotnet build EngineIndependentSaveSystem.slnx --no-restore
```

Run the test suite:

```bash
dotnet test EngineIndependentSaveSystem.slnx --no-build
```

### Testing

The project uses xUnit for automated testing.

The test suite covers:

- JSON serialization and deserialization.
- 2D and 3D position data.
- Save data validation.
- Invalid and malformed data.
- File creation and deletion.
- Missing files and directories.
- Maximum file-size enforcement.
- Concurrent operations.
- Cancellation behaviour.
- Preservation of previous saves when replacement fails.

## V1 Scope and Future Work

The current version focuses on establishing a reusable engine-independent save architecture with JSON serialization and local file storage.

The following features are deliberately outside the scope of V1:

- Cloud storage.
- Encryption.
- Compression.
- Multiple backup slots.
- Automatic saving.
- Save migrations.
- Stale temporary-file recovery.
- Cross-process locking.
- Game-engine-specific adapters.

These can be considered for future versions without expanding the responsibilities of the current implementation.