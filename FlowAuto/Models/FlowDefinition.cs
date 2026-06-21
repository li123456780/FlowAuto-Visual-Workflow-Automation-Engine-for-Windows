using System.Text.Json.Serialization;

namespace FlowAuto.Models;

public class FlowDefinition
{
    [JsonPropertyName("FlowName")]
    public string FlowName { get; set; } = "";

    [JsonPropertyName("Version")]
    public string Version { get; set; } = "1.0";

    [JsonPropertyName("CreatedAt")]
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");

    [JsonPropertyName("Nodes")]
    public List<FlowNode> Nodes { get; set; } = new();

    [JsonPropertyName("Connections")]
    public List<FlowConnection> Connections { get; set; } = new();
}
