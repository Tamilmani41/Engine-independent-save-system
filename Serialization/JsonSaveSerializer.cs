using System.Text.Json;
using System.Text.Json.Serialization;
using EngineIndependentSaveSystem.Abstractions;

namespace EngineIndependentSaveSystem.Serialization;

/// <summary>A generic, human-readable JSON serializer for save data.</summary>
public sealed class JsonSaveSerializer : ISaveSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public string Serialize<T>(T data)
    {
        ArgumentNullException.ThrowIfNull(data);
        try { return JsonSerializer.Serialize(data, Options); }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new InvalidOperationException($"Could not serialize save data of type '{typeof(T).Name}'.", exception);
        }
    }

    public T Deserialize<T>(string serializedData)
    {
        if (string.IsNullOrWhiteSpace(serializedData)) throw new InvalidDataException("Save data is empty.");
        try
        {
            return JsonSerializer.Deserialize<T>(serializedData, Options)
                ?? throw new InvalidDataException($"Save data did not contain a '{typeof(T).Name}' value.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Save data is not valid JSON for '{typeof(T).Name}'.", exception);
        }
    }
}
