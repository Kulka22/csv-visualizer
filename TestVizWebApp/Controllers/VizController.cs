using CsvHelper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json.Serialization;
using TestVizWebApp.Models.Requests;
using TestVizWebApp.Models.Responses;

namespace TestVizWebApp.Controllers;

/// <summary>
/// Контроллер для загрузки CSV и генерации столбчатых данных для визуализации.
/// </summary>
[ApiController]
[Route("api/V0")]
public class VizController : ControllerBase
{
    private class Dataset
    {
        public List<string> Headers { get; init; } = new();
        public List<Dictionary<string, string>> Rows { get; init; } = new();

        public Dataset(List<string> headers, List<Dictionary<string, string>> rows)
        {
            Headers = headers;
            Rows = rows;
        }
    }

    private static ConcurrentDictionary<int, Dataset> _datasets = new();
    private static int _datasetCounter = 0;

    /// <summary>
    /// Загрузить новый CSV-набор для анализа.
    /// </summary>
    /// <param name="file">CSV-файл</param>
    /// <returns>Результат загрузки, включая списки полей для категорий и мер.</returns>
    [HttpPost("dataset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UploadCsvAsync([Required] IFormFile file)
    {
        if (file == null || file.Length == 0)
            return NotFound("file loss!");

        if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            return BadRequest("Incorrect file extension, .csv is required!");

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);

        // тут я применил бесплатную библиотеку CsvHelper из пакетного менеджера NuGet,
        // чтобы не парсить полностью вручную csv-файл
        using var csvHelper = new CsvReader(reader, CultureInfo.InvariantCulture);

        if (!await csvHelper.ReadAsync())
            return NotFound("CSV is empty!");

        csvHelper.ReadHeader();
        var headers = csvHelper.HeaderRecord?.ToList() ?? new();

        List<Dictionary<string, string>> rows = new List<Dictionary<string, string>>();

        while (await csvHelper.ReadAsync())
        {
            Dictionary<string, string> row = new Dictionary<string, string>();
            bool hasEmpty = false;

            foreach (var header in headers)
            {
                var value = csvHelper.GetField(header);

                if (string.IsNullOrWhiteSpace(value))
                {
                    hasEmpty = true;
                    break;
                }

                row[header] = value;
            }

            if (!hasEmpty)
                rows.Add(row);
        }

        if (rows.Count == 0)
            return NotFound("Only headers in CSV!");

        List<string> categoryFields = new List<string>();
        List<string> measureFields = new List<string>();

        foreach (var header in headers)
        {
            bool isMeasureField = rows.All(row => double.TryParse
            (
                row[header],
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out _
            ));

            if (isMeasureField)
                measureFields.Add(header);
            else
                categoryFields.Add(header);
        }

        Dataset dataset = new Dataset(headers, rows);

        int datasetId = Interlocked.Increment(ref _datasetCounter);
        _datasets[datasetId] = dataset;

        return Ok(new UploadDatasetResponse
        {
            DatasetId = datasetId,
            Count = rows.Count,
            CategoryFields = categoryFields,
            MeasureFields = measureFields
        });
    }

    /// <summary>
    /// Получить агрегированные данные для столбчатой диаграммы.
    /// </summary>
    [HttpPost("viz/bar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<BarChartResponse> GetBarData([FromQuery(Name = "dataset_id")] int datasetId, [FromBody] BarChartRequest request)
    {
        Dataset currentDataset;

        if (!_datasets.TryGetValue(datasetId, out currentDataset))
            return NotFound();

        if (!currentDataset.Headers.Contains(request.CategoryField))
            return BadRequest("Invalid category field");

        if (!currentDataset.Headers.Contains(request.Measure.Field))
            return BadRequest("Invalid measure field");

        if (request.Top != null && request.Top.Count <= 0)
            return BadRequest("Top less than 0");

        IEnumerable<Dictionary<string, string>> rows = currentDataset.Rows.AsEnumerable();

        if (request.CategoryFilter != null && request.CategoryFilter.Count > 0)
        {
            rows = rows.Where(row =>
                request.CategoryFilter.Contains(row[request.CategoryField]));
        }

        List<BarChartItem> resultData = rows
            .GroupBy(row => row[request.CategoryField])
            .Select(group =>
            {
                double measure;
                switch (request.Measure.Aggregation)
                {
                    case AggregationType.SUM:
                        measure = group.Sum(g => double.Parse(g[request.Measure.Field],
                            CultureInfo.InvariantCulture));
                        break;
                    case AggregationType.AVG:
                        measure = group.Average(g => double.Parse(g[request.Measure.Field],
                            CultureInfo.InvariantCulture));
                        break;
                    case AggregationType.COUNT:
                        measure = group.Count();
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                return new BarChartItem()
                {
                    Category = group.Key,
                    Measure = measure
                };
            }).ToList();

        if (request.Top != null)
        {
            if (request.Top.Sort == SortOrder.DESC)
            {
                resultData = resultData
                    .OrderByDescending(r => r.Measure)
                    .Take(request.Top.Count)
                    .ToList();
            }
            else if (request.Top.Sort == SortOrder.ASC)
            {
                resultData = resultData
                    .OrderBy(r => r.Measure)
                    .Take(request.Top.Count)
                    .ToList();
            }
            else
                return BadRequest();
        }

        if (request.Sort != null)
        {
            switch (request.Sort.Field)
            {
                case SortField.measure:
                    if (request.Sort.Sort == SortOrder.DESC)
                        resultData = resultData.OrderByDescending(r => r.Measure).ToList();
                    else
                        resultData = resultData.OrderBy(r => r.Measure).ToList();
                    break;
                case SortField.category:
                    if (request.Sort.Sort == SortOrder.DESC)
                        resultData = resultData.OrderByDescending(r => r.Category).ToList();
                    else
                        resultData = resultData.OrderBy(r => r.Category).ToList();
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        if (request.ColorCondition != null)
        {
            if (request.ColorCondition.Comparison == ComparisonType.GT)
            {
                foreach (var item in resultData)
                {
                    if (item.Measure > request.ColorCondition.Value)
                        item.Color = request.ColorCondition.Color.ToString();
                }
            }
            else if (request.ColorCondition.Comparison == ComparisonType.LT)
            {
                foreach (var item in resultData)
                {
                    if (item.Measure < request.ColorCondition.Value)
                        item.Color = request.ColorCondition.Color.ToString();
                }
            }
            else if (request.ColorCondition.Comparison == ComparisonType.EQ)
            {
                foreach (var item in resultData)
                {
                    if (item.Measure == request.ColorCondition.Value)
                        item.Color = request.ColorCondition.Color.ToString();
                }
            }
        }

        double? refLine = request.ReferenceLine;

        if (refLine != null && resultData.Count > 0)
        {
            double minValue = resultData.Min(r => r.Measure);
            double maxValue = resultData.Max(r => r.Measure);
            if (request.ReferenceLine > maxValue)
                refLine = maxValue;
            else if (request.ReferenceLine < minValue)
                refLine = minValue;
        }

        return Ok(new BarChartResponse()
        {
            Data = resultData,
            ReferenceLine = refLine
        });
    }
}