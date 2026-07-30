using System.Globalization;
using UrbanPlanToolbox.Models;

namespace UrbanPlanToolbox.Helpers;

public static class ConversionResultFormatter
{
    public static string Format(ConversionResult result, int decimalPlaces, string symbol)
    {
        if (!result.IsSuccess || result.Value is null) return result.Error ?? "";
        var value = result.Value.Value;
        var fixedText = value.ToString($"N{decimalPlaces}", CultureInfo.CurrentCulture);
        if (value != 0 && decimal.TryParse(fixedText, NumberStyles.Number, CultureInfo.CurrentCulture, out var shown) && shown == 0)
            return $"{value.ToString("0.############################E+0", CultureInfo.CurrentCulture)} {symbol}";
        return $"{fixedText} {symbol}";
    }
}
