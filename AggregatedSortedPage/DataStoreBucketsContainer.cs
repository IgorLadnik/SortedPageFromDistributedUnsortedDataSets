using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace AggregatedSortedPage;

public interface IDataStoreBucketsContainer<T>
{
    Tuple<Dictionary<int, int>, int> GetBucketsLength();
    Dictionary<int, List<T>> GetBucketsContent(int beginBucket, int endBucket);
}

/// <summary>
/// Negative bucket keys exclude items. An item's sort key and bucket assignment must not
/// change while stored; remove and reinsert it. Returned lists copy membership, not T objects.
/// </summary>
public class DataStoreBucketsContainer<T> : IDataStoreBucketsContainer<T>
{
    private static long nextLockId;
    private readonly long lockId = Interlocked.Increment(ref nextLockId);
    private readonly Lock locker = new();
    private readonly Dictionary<int, List<T>> buckets = [];
    private readonly Func<T, int> determineBucket;

    public DataStoreBucketsContainer(IEnumerable<T> items, Func<T, int> determineItemBucket)
    {
        ArgumentNullException.ThrowIfNull(items);
        determineBucket = determineItemBucket ?? throw new ArgumentNullException(nameof(determineItemBucket));
        foreach (var item in items)
            AddItem(item);
    }

    public Tuple<Dictionary<int, int>, int> GetBucketsLength()
    {
        lock (locker)
        {
            var counts = buckets.ToDictionary(pair => pair.Key, pair => pair.Value.Count);
            int total = checked(counts.Values.Sum());
            return Tuple.Create(counts, total);
        }
    }

    public Dictionary<int, List<T>> GetBucketsContent(int beginBucket, int endBucket)
    {
        lock (locker)
            return buckets.Where(pair => pair.Key >= beginBucket && pair.Key <= endBucket)
                .ToDictionary(pair => pair.Key, pair => new List<T>(pair.Value));
    }

    public void AddItem(T item)
    {
        lock (locker)
        {
            int bucket = determineBucket(item);
            if (bucket < 0)
                return;
            if (!buckets.TryGetValue(bucket, out var items))
                buckets[bucket] = items = [];
            items.Add(item);
        }
    }

    public void RemoveItem(T item)
    {
        lock (locker)
        {
            int bucket = determineBucket(item);
            if (bucket >= 0 && buckets.TryGetValue(bucket, out var items))
                items.Remove(item);
        }
    }

    public Tuple<IList<int>, float, IList<float>> GetInfo()
    {
        var snapshot = GetBucketsLength();
        var sizes = snapshot.Item1.OrderBy(pair => pair.Key).Select(pair => pair.Value).ToList();
        float average = sizes.Count == 0 ? 0 : (float)snapshot.Item2 / sizes.Count;
        IList<float> ratios = sizes.Select(size => average == 0 ? 0 : size / average).ToList();
        return Tuple.Create<IList<int>, float, IList<float>>(sizes, average, ratios);
    }

    /// <summary>
    /// Hold all supplied in-process store locks across both query phases. AddItem and
    /// RemoveItem use these locks. Locks follow creation order and release in reverse.
    /// All relevant stores must participate; remote stores need external coordination.
    /// </summary>
    public static TResult WithAllLocked<TResult>(IEnumerable<DataStoreBucketsContainer<T>> stores,
        Func<TResult> operation)
    {
        ArgumentNullException.ThrowIfNull(stores);
        ArgumentNullException.ThrowIfNull(operation);
        var ordered = stores.ToArray();
        if (ordered.Any(store => store is null))
            throw new ArgumentException("Stores cannot contain null entries.", nameof(stores));
        ordered = ordered.Distinct().OrderBy(store => store.lockId).ToArray();
        int acquired = 0;
        try
        {
            foreach (var store in ordered)
            {
                store.locker.Enter();
                acquired++;
            }
            return operation();
        }
        finally
        {
            for (int i = acquired - 1; i >= 0; i--)
                ordered[i].locker.Exit();
        }
    }
}
