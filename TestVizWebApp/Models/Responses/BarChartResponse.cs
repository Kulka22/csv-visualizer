using System.Text.Json.Serialization;

namespace TestVizWebApp.Models.Responses
{
    public class BarChartItem
    {
        [JsonPropertyName("category")] public string Category { get; set; } = null!;

        [JsonPropertyName("measure")] public double Measure { get; set; }

        [JsonPropertyName("color")] public string? Color { get; set; }
    }

    public class BarChartResponse
    {
        [JsonPropertyName("data")] public List<BarChartItem> Data { get; set; } = new();

        [JsonPropertyName("reference_line")] public double? ReferenceLine { get; set; }
    }
}
