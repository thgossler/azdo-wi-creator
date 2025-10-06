using System.Text.Json;
using System.Text.Json.Serialization;

namespace AzDoWiCreator;

public class WorkItemSpec
{
    [JsonPropertyName("project")]
    public string? Project { get; set; }

    [JsonPropertyName("fields")]
    public Dictionary<string, object> Fields { get; set; } = new();

    [JsonPropertyName("areaPaths")]
    public List<string> AreaPaths { get; set; } = new();

    [JsonPropertyName("tags")]
    public string? Tags { get; set; }
}

public class WorkItemSpecFile
{
    [JsonPropertyName("workItems")]
    public List<WorkItemSpec> WorkItems { get; set; } = new();
}
