using System.Collections;

namespace UrbanPlanToolbox.Views;

internal static class ComboBoxItemsSourceExtensions
{
    // WinUI exposes ItemsSource as object. Keep LINQ-style Cast semantics available
    // to page-local filter helpers without relying on an implicit object-to-IEnumerable conversion.
    public static IEnumerable<T> Cast<T>(this object source)
    {
        if (source is not IEnumerable enumerable)
            yield break;

        foreach (var item in enumerable)
            yield return (T)item!;
    }
}
