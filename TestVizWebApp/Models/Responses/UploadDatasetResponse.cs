namespace TestVizWebApp.Models.Responses
{
    public class UploadDatasetResponse
    {
        public int DatasetId { get; set; }

        public int Count { get; set; }

        public List<string> CategoryFields { get; set; } = new();

        public List<string> MeasureFields { get; set; } = new();
    }
}
