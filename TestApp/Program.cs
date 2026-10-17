using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AggregatedSortedPage;

namespace MergeForPaging;

public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length > 1)
        {
            Console.Error.WriteLine("Usage: TestApp [path-to-text-file]");
            return 2;
        }
        string path = args.Length == 1 ? Path.GetFullPath(args[0]) :
            Path.Combine(AppContext.BaseDirectory, "t8.shakespeare.txt");
        if (!File.Exists(path))
        {
            Console.Error.WriteLine($"Input file not found: {path}");
            return 2;
        }
        try
        {
            // Match the original sample's symbol removal and space splitting.
            string cleaned = File.ReadAllText(path).ToLowerInvariant();
            const string removedCharacters = ".,\n\r\"'!?()*_-[]&><0123456789{}~:;\\";
            foreach (char symbol in removedCharacters)
                cleaned = cleaned.Replace(symbol.ToString(), "");
            var words = cleaned.Split(' ').Where(word => word.Length > 0 &&
                word[0] >= 'a' && word[0] <= 'z').ToList();
            if (words.Count == 0)
            {
                Console.Error.WriteLine($"No lowercase ASCII words found in {path}");
                return 2;
            }

            var all = words.Distinct(StringComparer.Ordinal).Select((word, index) => new Item
            {
                Word = word,
                DataStore = index % 3
            }).ToList();
            var stores = Enumerable.Range(0, 3)
                .Select(index => new DataStoreBucketsContainer<Item>(
                    all.Where(item => item.DataStore == index), ComparerItem.DetermineItemBucket))
                .ToArray();
            IList<IDataStoreBucketsContainer<Item>> proxies = stores.Cast<IDataStoreBucketsContainer<Item>>().ToList();
            var comparer = new ComparerItem();
            int offset = Math.Min(15000, Math.Max(0, all.Count - 1));
            var sorted = all.OrderBy(item => item, comparer).ToArray();
            int boundary = Enumerable.Range(1, sorted.Length - 1)
                .FirstOrDefault(index => ComparerItem.DetermineItemBucket(sorted[index - 1]) !=
                                         ComparerItem.DetermineItemBucket(sorted[index]));
            if (boundary == 0)
            {
                Console.Error.WriteLine("The input needs words in at least two buckets.");
                return 2;
            }
            if (!Check("initial page", offset, 25) ||
                !Check("page spanning buckets", Math.Max(0, boundary - 2), 5))
                return 1;

            var inserted = new Item { Word = "blueberry", DataStore = 1 };
            stores[1].AddItem(inserted);
            all.Add(inserted);
            if (!Check("after insertion", offset, 25))
                return 1;

            var removed = all[0];
            stores[removed.DataStore].RemoveItem(removed);
            all.Remove(removed);
            if (!Check("after removal", offset, 25))
                return 1;
            Console.WriteLine("All demo pages match the full-sort baseline.");
            return 0;

            bool Check(string label, int start, int length)
            {
                var actual = DataStoreBucketsContainer<Item>.WithAllLocked(stores,
                    () => Aggregator.GetPage(proxies, comparer,
                        new Query { Filter = start.ToString(System.Globalization.CultureInfo.InvariantCulture), Length = length }));
                var expected = all.OrderBy(item => item, comparer).Skip(start).Take(length).ToArray();
                // Equal words can exchange positions, so compare their sorted keys.
                bool equal = actual.Select(item => item.Word).SequenceEqual(expected.Select(item => item.Word));
                Console.WriteLine($"{label}: offset={start}, returned={actual.Length}: {(equal ? "PASS" : "FAIL")}");
                Console.WriteLine("Global index  Page index  Store  Word");
                for (int index = 0; index < actual.Length; index++)
                    Console.WriteLine($"{(long)start + index,12}  {index,10}  {actual[index].DataStore,5}  {actual[index].Word}");
                Console.WriteLine();
                return equal;
            }
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Demo failed: {exception.Message}");
            return 1;
        }
    }
}
