using System.Text.Json.Serialization;

namespace FlowAuto.Models;

/// <summary>
/// Defines a single detection target for the ColorCal node.
/// Each target has its own name, HSV color filter, region, and optional template matching rules.
/// </summary>
public class ColorCalTarget
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("TargetWindow")]
    public string TargetWindow { get; set; } = "";

    [JsonPropertyName("Region")]
    public Engine.Region Region { get; set; } = new() { X = 0, Y = 0, Width = 200, Height = 200 };

    [JsonPropertyName("UseFullScreen")]
    public bool UseFullScreen { get; set; } = true;

    // HSV Color filter
    [JsonPropertyName("TargetRgb")]
    public string TargetRgbStr { get; set; } = "49,218,183";

    [JsonPropertyName("HueTolerance")]
    public int HueTolerance { get; set; } = 8;

    [JsonPropertyName("SVTolerance")]
    public int SVTolerance { get; set; } = 30;

    // Detection mode: TemplateMatch (shape + color) or ColorTrack (color only)
    [JsonPropertyName("TrackMode")]
    public string TrackMode { get; set; } = "TemplateMatch";

    // Optional template matching
    [JsonPropertyName("TemplateImagePath")]
    public string TemplateImagePath { get; set; } = "";

    [JsonPropertyName("TemplateMatchThreshold")]
    public double TemplateMatchThreshold { get; set; } = 0.8;

    /// <summary>
    /// Get the RGB color from the stored string.
    /// </summary>
    public System.Drawing.Color GetRgbColor()
    {
        var parts = TargetRgbStr.Split(',');
        if (parts.Length == 3 &&
            int.TryParse(parts[0], out int r) &&
            int.TryParse(parts[1], out int g) &&
            int.TryParse(parts[2], out int b))
        {
            return System.Drawing.Color.FromArgb(r, g, b);
        }
        return System.Drawing.Color.FromArgb(49, 218, 183);
    }

    public void SetRgbColor(System.Drawing.Color color)
    {
        TargetRgbStr = $"{color.R},{color.G},{color.B}";
    }
}

/// <summary>
/// Detection result for a single ColorCalTarget.
/// </summary>
public class ColorCalTargetResult
{
    [JsonPropertyName("Name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("Found")]
    public bool Found { get; set; }

    [JsonPropertyName("X")]
    public int X { get; set; }

    [JsonPropertyName("Y")]
    public int Y { get; set; }

    [JsonPropertyName("Confidence")]
    public double Confidence { get; set; }
}
