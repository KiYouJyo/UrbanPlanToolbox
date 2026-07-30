namespace UrbanPlanToolbox.Helpers;

public static class NumberFormatter
{
    public static string Value(decimal? value, int places, string suffix = "") => value is null ? "—" : $"{value.Value.ToString($"N{places}")}{suffix}";
    public static string Percent(decimal? value, int places) => Value(value, places, "%");
}
