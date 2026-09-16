namespace Moria.Core;

internal static class CollectionExtensions
{
    internal static int IndexOf<T>(this IReadOnlyList<T> items, T value)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        for (int i = 0; i < items.Count; i++)
        {
            if (comparer.Equals(items[i], value))
                return i;
        }

        return -1;
    }
}
