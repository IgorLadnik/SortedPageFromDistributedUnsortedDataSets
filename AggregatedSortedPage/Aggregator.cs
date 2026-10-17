using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AggregatedSortedPage;

/// <summary>
/// For included a and b, Bucket(a) &lt; Bucket(b) must imply comparer.Compare(a, b) &lt;= 0.
/// Concurrent updates across the two query phases require external coordination.
/// </summary>
public static class Aggregator
{
    public static T[] GetPage<T>(IList<IDataStoreBucketsContainer<T>> dataStoreProxies,
        IComparer<T> comparerT, Query query)
    {
        ArgumentNullException.ThrowIfNull(dataStoreProxies);
        ArgumentNullException.ThrowIfNull(comparerT);
        ArgumentNullException.ThrowIfNull(query);
        if (query.Length <= 0)
            throw new ArgumentOutOfRangeException(nameof(query), "Page length must be positive.");
        if (!int.TryParse(query.Filter, NumberStyles.None, CultureInfo.InvariantCulture, out int offset))
            throw new ArgumentException("Filter must be a nonnegative Int32 decimal offset.", nameof(query));

        var counts = new SortedDictionary<int, long>();
        foreach (var store in dataStoreProxies)
        {
            if (store is null)
                throw new ArgumentException("Stores cannot contain null entries.", nameof(dataStoreProxies));
            var snapshot = store.GetBucketsLength();
            if (snapshot is null || snapshot.Item1 is null || snapshot.Item2 < 0)
                throw new InvalidOperationException("A store returned invalid bucket counts.");
            long storeTotal = 0;
            foreach (var (key, count) in snapshot.Item1)
            {
                if (count < 0)
                    throw new InvalidOperationException("A store returned a negative bucket count.");
                storeTotal = checked(storeTotal + count);
                if (key >= 0)
                    counts[key] = checked(counts.GetValueOrDefault(key) + count);
            }
            if (storeTotal != snapshot.Item2)
                throw new InvalidOperationException("Store total differs from its bucket counts.");
        }

        long cumulative = 0;
        int? first = null;
        int? last = null;
        long localOffset = 0;
        long lastRequestedIndex = (long)offset + query.Length - 1;
        foreach (var (key, count) in counts)
        {
            long previous = cumulative;
            cumulative = checked(cumulative + count);
            if (first is null && cumulative > offset)
            {
                first = key;
                localOffset = offset - previous;
            }
            if (first is not null && cumulative > lastRequestedIndex)
            {
                last = key;
                break;
            }
        }
        if (first is null)
            return [];
        last ??= counts.Last(pair => pair.Value > 0).Key;

        var selected = new SortedDictionary<int, List<T>>();
        foreach (var store in dataStoreProxies)
        {
            foreach (var (key, items) in store.GetBucketsContent(first.Value, last.Value))
            {
                if (key < first || key > last || items is null)
                    throw new InvalidOperationException("A store returned invalid bucket content.");
                if (!selected.TryGetValue(key, out var aggregate))
                    selected[key] = aggregate = [];
                aggregate.AddRange(items);
            }
        }

        var result = new List<T>();
        long toSkip = localOffset;
        foreach (var (_, items) in selected)
        {
            items.Sort(comparerT);
            foreach (var item in items)
            {
                if (toSkip > 0)
                    toSkip--;
                else if (result.Count < query.Length)
                    result.Add(item);
                else
                    return result.ToArray();
            }
        }
        return result.ToArray();
    }
}

public class Query
{
    /// <summary>Zero-based nonnegative Int32 offset in invariant decimal notation.</summary>
    public string? Filter { get; set; }
    public int Length { get; set; }
}
