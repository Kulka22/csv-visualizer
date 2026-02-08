using System.Text.Json.Serialization;
using TestVizWebApp.Controllers;

namespace TestVizWebApp.Models.Requests
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AggregationType
    {
        SUM,
        AVG,
        COUNT
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SortOrder
    {
        ASC,
        DESC
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SortField
    {
        measure,
        category
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ColorType
    {
        RED,
        BLUE,
        MAGENTA
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ComparisonType
    {
        GT, // >
        LT, // <
        EQ  // ==
    }

    public class BarChartRequest
    {
        [JsonPropertyName("category_field")] public string CategoryField { get; set; } = null!;

        [JsonPropertyName("measure")] public MeasureRequest Measure { get; set; } = null!;

        [JsonPropertyName("category_filter")] public List<string>? CategoryFilter { get; set; }

        [JsonPropertyName("top")] public TopRequest? Top { get; set; }

        [JsonPropertyName("sort")] public SortRequest? Sort { get; set; }

        [JsonPropertyName("color_condition")] public ColorConditionRequest? ColorCondition { get; set; }

        [JsonPropertyName("reference_line")] public double? ReferenceLine { get; set; }
    }


    public class MeasureRequest
    {
        public string Field { get; set; } = null!;

        public AggregationType Aggregation { get; set; }
    }

    public class TopRequest
    {
        public int Count { get; set; }
        public SortOrder Sort { get; set; }
    }

    public class SortRequest
    {
        public SortField Field { get; set; }

        public SortOrder Sort { get; set; }
    }

    public class ColorConditionRequest
    {
        public ColorType Color { get; set; }

        public ComparisonType Comparison { get; set; }

        public double Value { get; set; }
    }
}
