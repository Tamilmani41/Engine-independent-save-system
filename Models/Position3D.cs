using System.Text.Json.Serialization;

namespace EngineIndependentSaveSystem.Models;

/// <summary>An engine-independent three-dimensional position.</summary>
public sealed record Position3D(
    [property: JsonRequired] float X,
    [property: JsonRequired] float Y,
    [property: JsonRequired] float Z);
