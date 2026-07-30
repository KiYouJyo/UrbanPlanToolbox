using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Services;

public sealed class UnitConversionService
{
    public static IReadOnlyList<MeasurementUnit> Units { get; } =
    [
        new("length-mm", "毫米（mm，公制）", "mm", MeasurementCategory.Length, "公制", 1, 1000, 10),
        new("length-cm", "厘米（cm，公制）", "cm", MeasurementCategory.Length, "公制", 1, 100, 20),
        new("length-m", "米（m，公制）", "m", MeasurementCategory.Length, "公制", 1, 1, 30),
        new("length-km", "千米（km，公制）", "km", MeasurementCategory.Length, "公制", 1000, 1, 40),
        new("length-in", "英寸（in，英美）", "in", MeasurementCategory.Length, "英美", 254, 10000, 50),
        new("length-ft", "英尺（ft，英美）", "ft", MeasurementCategory.Length, "英美", 3048, 10000, 60),
        new("length-yd", "码（yd，英美）", "yd", MeasurementCategory.Length, "英美", 9144, 10000, 70),
        new("length-mi", "英里（mi，英美）", "mi", MeasurementCategory.Length, "英美", 1609344, 1000, 80),
        new("length-shaku", "尺（しゃく，日本传统建筑单位）", "尺", MeasurementCategory.Length, "日本", 10, 33, 90, true),
        new("length-ken", "间（けん，日本传统建筑单位）", "間", MeasurementCategory.Length, "日本", 20, 11, 100, true),

        new("area-mm2", "平方毫米（mm²，公制）", "mm²", MeasurementCategory.Area, "公制", 1, 1_000_000, 10),
        new("area-cm2", "平方厘米（cm²，公制）", "cm²", MeasurementCategory.Area, "公制", 1, 10_000, 20),
        new("area-m2", "平方米（m²，公制）", "m²", MeasurementCategory.Area, "公制", 1, 1, 30),
        new("area-ha", "公顷（ha，公制）", "ha", MeasurementCategory.Area, "公制", 10_000, 1, 40),
        new("area-km2", "平方千米（km²，公制）", "km²", MeasurementCategory.Area, "公制", 1_000_000, 1, 50),
        new("area-mu", "亩（中国）", "亩", MeasurementCategory.Area, "中国", 2000, 3, 60, true),
        new("area-tsubo", "坪（日本）", "坪", MeasurementCategory.Area, "日本", 400, 121, 70, true),
        new("area-tan", "反（日本）", "反", MeasurementCategory.Area, "日本", 120000, 121, 80, true),
        new("area-cho", "町（日本）", "町", MeasurementCategory.Area, "日本", 1200000, 121, 90, true),
        new("area-in2", "平方英寸（in²，英美）", "in²", MeasurementCategory.Area, "英美", 64516, 100000000, 100),
        new("area-ft2", "平方英尺（ft²，英美）", "ft²", MeasurementCategory.Area, "英美", 9290304, 100000000, 110),
        new("area-yd2", "平方码（yd²，英美）", "yd²", MeasurementCategory.Area, "英美", 83612736, 100000000, 120),
        new("area-acre", "英亩（acre，英美）", "acre", MeasurementCategory.Area, "英美", 40468564224, 10000000, 130),
        new("area-mi2", "平方英里（mi²，英美）", "mi²", MeasurementCategory.Area, "英美", 2589988110336, 1000000, 140),

        new("volume-mm3", "立方毫米（mm³，公制）", "mm³", MeasurementCategory.Volume, "公制", 1, 1_000_000_000, 10),
        new("volume-cm3", "立方厘米（cm³，公制）", "cm³", MeasurementCategory.Volume, "公制", 1, 1_000_000, 20),
        new("volume-m3", "立方米（m³，公制）", "m³", MeasurementCategory.Volume, "公制", 1, 1, 30),
        new("volume-l", "升（L，几何体积）", "L", MeasurementCategory.Volume, "公制", 1, 1000, 40),
        new("volume-in3", "立方英寸（in³，英美）", "in³", MeasurementCategory.Volume, "英美", 16387064, 1000000000000, 50),
        new("volume-ft3", "立方英尺（ft³，英美）", "ft³", MeasurementCategory.Volume, "英美", 28316846592, 1000000000000, 60),
        new("volume-yd3", "立方码（yd³，英美）", "yd³", MeasurementCategory.Volume, "英美", 764554857984, 1000000000000, 70)
    ];

    public IReadOnlyList<MeasurementUnit> GetUnits(MeasurementCategory category) => Units.Where(unit => unit.Category == category).OrderBy(unit => unit.SortOrder).ToArray();

    public ConversionResult Convert(decimal value, string? sourceId, string? targetId)
    {
        if (value < 0) return ConversionResult.Failure("输入数值不能为负数。");
        var source = Units.SingleOrDefault(unit => unit.Id == sourceId);
        var target = Units.SingleOrDefault(unit => unit.Id == targetId);
        if (source is null || target is null) return ConversionResult.Failure("请选择来源单位和目标单位。");
        if (source.Category != target.Category) return ConversionResult.Failure("来源单位和目标单位必须属于同一类别。");
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
        catch (OverflowException) { return ConversionResult.Failure("数值超出可处理范围。"); }
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
