using System.Text.Json.Serialization;

namespace EngineIndependentSaveSystem.Models;

/// <summary>An engine-independent two-dimensional position.</summary>
public sealed record Position2D(
    [property: JsonRequired] float X,
    [property: JsonRequired] float Y);
