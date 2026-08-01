using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class UnitConversionService
{
    private readonly ILocalizationService _localization;

    public UnitConversionService(ILocalizationService localization)
    {
        _localization = localization ?? throw new ArgumentNullException(nameof(localization));
    }

    public static IReadOnlyList<MeasurementUnit> Units { get; } =
    [
        new("length-mm", "Unit_LengthMm_Name", "mm", MeasurementCategory.Length, "metric", 1, 1000, 10),
        new("length-cm", "Unit_LengthCm_Name", "cm", MeasurementCategory.Length, "metric", 1, 100, 20),
        new("length-m", "Unit_LengthM_Name", "m", MeasurementCategory.Length, "metric", 1, 1, 30),
        new("length-km", "Unit_LengthKm_Name", "km", MeasurementCategory.Length, "metric", 1000, 1, 40),
        new("length-in", "Unit_LengthIn_Name", "in", MeasurementCategory.Length, "imperial", 254, 10000, 50),
        new("length-ft", "Unit_LengthFt_Name", "ft", MeasurementCategory.Length, "imperial", 3048, 10000, 60),
        new("length-yd", "Unit_LengthYd_Name", "yd", MeasurementCategory.Length, "imperial", 9144, 10000, 70),
        new("length-mi", "Unit_LengthMi_Name", "mi", MeasurementCategory.Length, "imperial", 1609344, 1000, 80),
        new("length-shaku", "Unit_LengthShaku_Name", "尺", MeasurementCategory.Length, "japanese", 10, 33, 90, true),
        new("length-ken", "Unit_LengthKen_Name", "間", MeasurementCategory.Length, "japanese", 20, 11, 100, true),

        new("area-mm2", "Unit_AreaMm2_Name", "mm²", MeasurementCategory.Area, "metric", 1, 1_000_000, 10),
        new("area-cm2", "Unit_AreaCm2_Name", "cm²", MeasurementCategory.Area, "metric", 1, 10_000, 20),
        new("area-m2", "Unit_AreaM2_Name", "m²", MeasurementCategory.Area, "metric", 1, 1, 30),
        new("area-ha", "Unit_AreaHa_Name", "ha", MeasurementCategory.Area, "metric", 10_000, 1, 40),
        new("area-km2", "Unit_AreaKm2_Name", "km²", MeasurementCategory.Area, "metric", 1_000_000, 1, 50),
        new("area-mu", "Unit_AreaMu_Name", "亩", MeasurementCategory.Area, "chinese", 2000, 3, 60, true),
        new("area-tsubo", "Unit_AreaTsubo_Name", "坪", MeasurementCategory.Area, "japanese", 400, 121, 70, true),
        new("area-tan", "Unit_AreaTan_Name", "反", MeasurementCategory.Area, "japanese", 120000, 121, 80, true),
        new("area-cho", "Unit_AreaCho_Name", "町", MeasurementCategory.Area, "japanese", 1200000, 121, 90, true),
        new("area-in2", "Unit_AreaIn2_Name", "in²", MeasurementCategory.Area, "imperial", 64516, 100000000, 100),
        new("area-ft2", "Unit_AreaFt2_Name", "ft²", MeasurementCategory.Area, "imperial", 9290304, 100000000, 110),
        new("area-yd2", "Unit_AreaYd2_Name", "yd²", MeasurementCategory.Area, "imperial", 83612736, 100000000, 120),
        new("area-acre", "Unit_AreaAcre_Name", "acre", MeasurementCategory.Area, "imperial", 40468564224, 10000000, 130),
        new("area-mi2", "Unit_AreaMi2_Name", "mi²", MeasurementCategory.Area, "imperial", 2589988110336, 1000000, 140),

        new("volume-mm3", "Unit_VolumeMm3_Name", "mm³", MeasurementCategory.Volume, "metric", 1, 1_000_000_000, 10),
        new("volume-cm3", "Unit_VolumeCm3_Name", "cm³", MeasurementCategory.Volume, "metric", 1, 1_000_000, 20),
        new("volume-m3", "Unit_VolumeM3_Name", "m³", MeasurementCategory.Volume, "metric", 1, 1, 30),
        new("volume-l", "Unit_VolumeL_Name", "L", MeasurementCategory.Volume, "metric", 1, 1000, 40),
        new("volume-in3", "Unit_VolumeIn3_Name", "in³", MeasurementCategory.Volume, "imperial", 16387064, 1000000000000, 50),
        new("volume-ft3", "Unit_VolumeFt3_Name", "ft³", MeasurementCategory.Volume, "imperial", 28316846592, 1000000000000, 60),
        new("volume-yd3", "Unit_VolumeYd3_Name", "yd³", MeasurementCategory.Volume, "imperial", 764554857984, 1000000000000, 70)
    ];

    public IReadOnlyList<MeasurementUnit> GetUnits(MeasurementCategory category) => Units.Where(unit => unit.Category == category).OrderBy(unit => unit.SortOrder).ToArray();

    public ConversionResult Convert(decimal value, string? sourceId, string? targetId)
    {
        if (value < 0) return ConversionResult.Failure(_localization.GetString("Error_NegativeInput"));
        var source = Units.SingleOrDefault(unit => unit.Id == sourceId);
        var target = Units.SingleOrDefault(unit => unit.Id == targetId);
        if (source is null || target is null) return ConversionResult.Failure(_localization.GetString("Error_SelectUnits"));
        if (source.Category != target.Category) return ConversionResult.Failure(_localization.GetString("Error_CrossCategoryUnits"));
        try
        {
            var sourceNumerator = source.Numerator;
            var sourceDenominator = source.Denominator;
            var targetNumerator = target.Numerator;
            var targetDenominator = target.Denominator;
            Reduce(ref sourceNumerator, ref sourceDenominator);
            Reduce(ref targetDenominator, ref targetNumerator);
            Reduce(ref sourceNumerator, ref targetNumerator);
            Reduce(ref targetDenominator, ref sourceDenominator);
            var numerator = sourceNumerator * targetDenominator;
            var denominator = sourceDenominator * targetNumerator;
            return ConversionResult.Success(value <= decimal.MaxValue / numerator ? value * numerator / denominator : value / denominator * numerator);
        }
        catch (OverflowException) { return ConversionResult.Failure(_localization.GetString("Error_OutOfRange")); }
    }

    private static void Reduce(ref decimal numerator, ref decimal denominator)
    {
        var divisor = GreatestCommonDivisor(numerator, denominator);
        numerator /= divisor;
        denominator /= divisor;
    }

    private static decimal GreatestCommonDivisor(decimal left, decimal right)
    {
        while (right != 0) (left, right) = (right, decimal.Remainder(left, right));
        return left;
    }
}
