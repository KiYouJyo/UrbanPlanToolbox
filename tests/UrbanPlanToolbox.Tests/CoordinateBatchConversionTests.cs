using UrbanPlanToolbox.Models;
using UrbanPlanToolbox.Services;
using Xunit;

namespace UrbanPlanToolbox.Tests;

public sealed class CoordinateBatchConversionTests
{
    private readonly CoordinateBatchConversionService _service = new();

    [Fact]
    public void ParsesDecimalDegreesAndAutoOrder()
    {
        var result = _service.ParsePair("120.1532,30.2741");
        Assert.True(result.IsSuccess); Assert.Equal(120.1532, result.Coordinate!.Longitude, 6); Assert.Equal(30.2741, result.Coordinate.Latitude, 6);
    }

    [Fact]
    public void ParsesDmsDdmAndDirections()
    {
        var dms = _service.ParsePair("120°09′11.52″E,30°16′26.76″N");
        var ddm = _service.ParsePair("120°09.192′E,30°16.446′N");
        var chinese = _service.ParsePair("西经120°09′11.52″,南纬30°16′26.76″");
        Assert.Equal(CoordinateTextFormat.DegreesMinutesSeconds, dms.DetectedFormat); Assert.Equal(120.1532, dms.Coordinate!.Longitude, 4);
        Assert.Equal(CoordinateTextFormat.DegreesDecimalMinutes, ddm.DetectedFormat); Assert.Equal(30.2741, ddm.Coordinate!.Latitude, 4);
        Assert.Equal(-120.1532, chinese.Coordinate!.Longitude, 4); Assert.Equal(-30.2741, chinese.Coordinate.Latitude, 4);
    }

    [Fact]
    public void NormalizesUnicodeSymbolsAndRejectsDirectionConflict()
    {
        Assert.Equal("120°09′11.52″E", CoordinateBatchConversionService.Normalize("120º09'11.52\"E"));
        var result = _service.ParsePair("-120.15E,30.2N");
        Assert.False(result.IsSuccess); Assert.Contains("conflicts", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DetectsLatLonAndAmbiguousOrder()
    {
        var swapped = _service.ParsePair("30.27,120.15");
        var ambiguous = _service.ParsePair("35.1,42.8");
        Assert.True(swapped.IsSuccess); Assert.Equal(120.15, swapped.Coordinate!.Longitude, 3); Assert.Equal(CoordinateRowStatus.Warning, swapped.Status);
        Assert.False(ambiguous.IsSuccess); Assert.Equal(CoordinateRowStatus.Warning, ambiguous.Status); Assert.Contains("Ambiguous", ambiguous.Message);
    }

    [Fact]
    public void ValidatesLatitudeAndFormatsAllOutputForms()
    {
        var invalid = _service.ParsePair("120,95", CoordinateOrder.LongitudeLatitude);
        var coordinate = new NormalizedCoordinate(120.1532, 30.2741);
        Assert.Contains("Latitude", invalid.Message); Assert.Equal("120.153200,30.274100", CoordinateBatchConversionService.Format(coordinate, CoordinateTextFormat.DecimalDegrees));
        Assert.Contains("°", CoordinateBatchConversionService.Format(coordinate, CoordinateTextFormat.DegreesDecimalMinutes));
        Assert.Contains("″", CoordinateBatchConversionService.Format(coordinate, CoordinateTextFormat.DegreesMinutesSeconds));
    }

    [Fact]
    public void InvalidRowsDoNotAbortBatchAndPreserveFields()
    {
        var rows = new[]
        {
            (IReadOnlyDictionary<string,string>)new Dictionary<string,string> { ["ID"] = "A1", ["Name"] = "Name, With Comma", ["Coordinate"] = "120.1,30.2" },
            new Dictionary<string,string> { ["ID"] = "A2", ["Name"] = "Bad", ["Coordinate"] = "120°99′N,30°2′N" }
        };
        var result = _service.ParseRows(rows, null, null, "Coordinate");
        Assert.Equal(2, result.Total); Assert.Equal(1, result.SuccessCount); Assert.Equal("Name, With Comma", result.Rows[0].Fields["Name"]); Assert.Equal(CoordinateRowStatus.Error, result.Rows[1].Result.Status);
    }

    [Fact]
    public void ParsesQuotedCsvFieldsWithoutSplittingEmbeddedComma()
    {
        var rows = CoordinateBatchConversionService.ParseDelimited("ID,Name,Coordinate\r\nA1,\"Name, With Comma\",\"120.1,30.2\"\r\n", ',');
        Assert.Equal("Name, With Comma", rows[0]["Name"]); Assert.Equal("120.1,30.2", rows[0]["Coordinate"]);
    }

    [Fact]
    public void OutputFormatAndPrecisionChangeRenderedValuesAndCsv()
    {
        var coordinate = new NormalizedCoordinate(120.1532, 30.2741);
        Assert.Equal("120.15320", CoordinateBatchConversionService.FormatSingle(coordinate.Longitude, true, CoordinateTextFormat.DecimalDegrees, 5));
        Assert.Equal("120°9.192′E", CoordinateBatchConversionService.FormatSingle(coordinate.Longitude, true, CoordinateTextFormat.DegreesDecimalMinutes, 3));
        var row = new CoordinateBatchRow("A1", "120.1532,30.2741", new Dictionary<string, string>(), new(true, coordinate, CoordinateTextFormat.DecimalDegrees, CoordinateRowStatus.Success, ""));
        var csv = CoordinateBatchConversionService.ExportCsv([row], CoordinateTextFormat.DegreesMinutesSeconds, 2);
        Assert.Contains("120°09′11.52″E", csv); Assert.Contains("30°16′26.76″N", csv);
    }
}
