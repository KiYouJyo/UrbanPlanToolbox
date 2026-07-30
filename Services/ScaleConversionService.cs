using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class ScaleConversionService
{
    private static readonly IReadOnlyDictionary<string, decimal> ToMeters = new Dictionary<string, decimal>
    {
        ["mm"] = 0.001m, ["cm"] = 0.01m, ["m"] = 1m, ["km"] = 1000m
    };

    public ConversionResult DrawingToActual(decimal denominator, decimal drawingLength, string drawingUnit, string actualUnit)
        => Convert(denominator, drawingLength, drawingUnit, actualUnit, forward: true);
    public ConversionResult ActualToDrawing(decimal denominator, decimal actualLength, string actualUnit, string drawingUnit)
        => Convert(denominator, actualLength, actualUnit, drawingUnit, forward: false);

    private static ConversionResult Convert(decimal denominator, decimal length, string sourceUnit, string targetUnit, bool forward)
    {
        if (denominator <= 0) return ConversionResult.Failure("比例尺分母必须大于 0。");
        if (length < 0) return ConversionResult.Failure("长度不能为负数。");
        if (!ToMeters.TryGetValue(sourceUnit, out var sourceFactor) || !ToMeters.TryGetValue(targetUnit, out var targetFactor)) return ConversionResult.Failure("请选择有效的长度单位。");
        try
        {
            var meters = length * sourceFactor;
            return ConversionResult.Success((forward ? meters * denominator : meters / denominator) / targetFactor);
        }
        catch (OverflowException) { return ConversionResult.Failure("数值超出可处理范围。"); }
    }
}
