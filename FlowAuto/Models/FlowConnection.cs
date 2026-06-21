using System.Text.Json.Serialization;

namespace FlowAuto.Models;

/// <summary>
/// Represents a connection between two nodes in the flow canvas.
/// Supports multiple output ports per node (standard, true, false).
/// Supports multiple input ports for Gate nodes (Input0, Input1, Input2).
/// </summary>
public class FlowConnection
{
    [JsonPropertyName("FromId")]
    public string FromId { get; set; } = "";

    [JsonPropertyName("ToId")]
    public string ToId { get; set; } = "";

    /// <summary>
    /// Output port name: "Output" for standard nodes, "True" or "False" for Condition/Gate/Loop nodes.
    /// </summary>
    [JsonPropertyName("FromPort")]
    public string FromPort { get; set; } = "Output";

    /// <summary>
    /// Input port name: "Input" for standard nodes, "Input0"/"Input1"/"Input2" for Gate nodes.
    /// </summary>
    [JsonPropertyName("ToPort")]
    public string ToPort { get; set; } = "Input";
}
