namespace EngineIndependentSaveSystem.Abstractions;

/// <summary>Converts data to and from a string representation.</summary>
public interface ISaveSerializer
{
    string Serialize<T>(T data);
    T Deserialize<T>(string serializedData);
}
