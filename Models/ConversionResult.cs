namespace UrbanPlanToolbox.Models;

public sealed class ConversionResult
{
    public decimal? Value { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => Error is null;

    public static ConversionResult Success(decimal value) => new() { Value = value };
    public static ConversionResult Failure(string error) => new() { Error = error };
}
