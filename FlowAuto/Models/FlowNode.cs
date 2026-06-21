using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlowAuto.Models;

public class FlowNode
{
    [JsonPropertyName("NodeId")]
    public string NodeId { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("NodeType")]
    public NodeType NodeType { get; set; }

    [JsonPropertyName("NodeName")]
    public string NodeName { get; set; } = "";

    [JsonPropertyName("Enabled")]
    public bool Enabled { get; set; } = true;

    [JsonPropertyName("TimeoutMs")]
    public int TimeoutMs { get; set; } = 30000;

    [JsonPropertyName("RetryCount")]
    public int RetryCount { get; set; } = 3;

    [JsonPropertyName("Parameters")]
    public Dictionary<string, object?> Parameters { get; set; } = new();

    // For UI positioning
    [JsonPropertyName("CanvasX")]
    public float CanvasX { get; set; }

    [JsonPropertyName("CanvasY")]
    public float CanvasY { get; set; }

    // For Loop node
    [JsonPropertyName("Children")]
    public List<FlowNode> Children { get; set; } = new();

    /// <summary>
    /// Link from LoopEnd back to its paired LoopStart node.
    /// Used so the executor can find body nodes and loop parameters.
    /// </summary>
    [JsonPropertyName("PairedLoopStartId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? PairedLoopStartId { get; set; }

    // For Condition branches
    [JsonPropertyName("TrueBranch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FlowNode>? TrueBranch { get; set; }

    [JsonPropertyName("FalseBranch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FlowNode>? FalseBranch { get; set; }

    /// <summary>
    /// Third branch for Condition nodes (e.g., ImageMove center/neutral position).
    /// </summary>
    [JsonPropertyName("CenterBranch")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<FlowNode>? CenterBranch { get; set; }

    /// <summary>
    /// Result branches for ColorCal node. Key = result label (e.g., "0", "1", "Default"), Value = branch nodes.
    /// </summary>
    [JsonPropertyName("ResultBranches")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<FlowNode>>? ResultBranches { get; set; }

    /// <summary>
    /// Direction branches for ColorMotion DirectionDetect mode. Key = direction name, Value = branch nodes.
    /// </summary>
    [JsonPropertyName("DirectionBranches")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, List<FlowNode>>? DirectionBranches { get; set; }

    // Helper to get typed parameter
    public T? GetParam<T>(string key)
    {
        if (Parameters.TryGetValue(key, out var val) && val is JsonElement je)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(je.GetRawText());
            }
            catch
            {
                // Corrupted value (e.g. empty object {} where a number was expected)
                return default;
            }
        }
        if (val is T tVal) return tVal;
        return default;
    }

    public void SetParam(string key, object? value)
    {
        // Defensive: reject JsonElement values that are empty objects
        if (value is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object)
            return;
        Parameters[key] = value;
    }

    /// <summary>
    /// Resolve a target RGB colour from node parameters.
    /// Reads from a plain string "R,G,B" first (survives JSON round-trip),
    /// falls back to a stored Color object (legacy), then to the default.
    /// </summary>
    public System.Drawing.Color ResolveTargetRgb(string key = "TargetRgb", System.Drawing.Color? defaultColor = null)
    {
        var fallback = defaultColor ?? System.Drawing.Color.FromArgb(49, 218, 183);

        // 1) Try the plain string (always survives JSON)
        var str = GetParam<string>(key);
        if (!string.IsNullOrEmpty(str))
        {
            var parts = str.Split(',');
            if (parts.Length == 3 &&
                int.TryParse(parts[0].Trim(), out int r) &&
                int.TryParse(parts[1].Trim(), out int g) &&
                int.TryParse(parts[2].Trim(), out int b))
                return System.Drawing.Color.FromArgb(r, g, b);
        }

        // 2) Legacy: direct Color object (may fail after JSON round-trip)
        var col = GetParam<System.Drawing.Color?>(key);
        if (col.HasValue && col.Value != System.Drawing.Color.Empty)
            return col.Value;

        return fallback;
    }
}
