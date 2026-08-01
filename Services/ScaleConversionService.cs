using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class ScaleConversionService
{
    private readonly ILocalizationService _localization;

    public ScaleConversionService(ILocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    private static readonly IReadOnlyDictionary<string, decimal> ToMeters = new Dictionary<string, decimal>
    {
        ["mm"] = 0.001m, ["cm"] = 0.01m, ["m"] = 1m, ["km"] = 1000m
    };

    public ConversionResult DrawingToActual(decimal denominator, decimal drawingLength, string drawingUnit, string actualUnit)
        => Convert(denominator, drawingLength, drawingUnit, actualUnit, forward: true);
    public ConversionResult ActualToDrawing(decimal denominator, decimal actualLength, string actualUnit, string drawingUnit)
        => Convert(denominator, actualLength, actualUnit, drawingUnit, forward: false);

    private ConversionResult Convert(decimal denominator, decimal length, string sourceUnit, string targetUnit, bool forward)
    {
        if (denominator <= 0) return ConversionResult.Failure(_localization.GetString("Error_ScaleDenominatorPositive"));
        if (length < 0) return ConversionResult.Failure(_localization.GetString("Error_NegativeLength"));
        if (!ToMeters.TryGetValue(sourceUnit, out var sourceFactor) || !ToMeters.TryGetValue(targetUnit, out var targetFactor)) return ConversionResult.Failure(_localization.GetString("Error_SelectValidLengthUnit"));
        try
        {
            var meters = length * sourceFactor;
            return ConversionResult.Success((forward ? meters * denominator : meters / denominator) / targetFactor);
        }
        catch (OverflowException) { return ConversionResult.Failure(_localization.GetString("Error_OutOfRange")); }
    }
}
