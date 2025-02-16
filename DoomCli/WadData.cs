using System.Text.Json.Serialization;

namespace DoomCli;

[JsonDerivedType(typeof(WadData))]
public interface IWadData
{
    string Name { get; }
    string? Title { get; }
    bool IsIwad { get; }
    string? IwadName { get; }
    string? Complevel { get; }
    string? ComplevelHint { get; }
}

public class WadData : IWadData
{
    public required string Name { get; init; }
    public string? Title { get; set; }
    public bool IsIwad { get; set; }
    public string? IwadName { get; set; }
    public string? Complevel { get; set; }
    public string? ComplevelHint { get; set; }
}