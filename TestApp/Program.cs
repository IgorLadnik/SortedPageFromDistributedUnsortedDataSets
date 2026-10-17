using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using AggregatedSortedPage;

namespace MergeForPaging
{
    public class Program
    {
        // Hardcoded for the test
        const string textFileName = "t8.shakespeare.txt";
        const int numOfDataStores = 3;

        static IList<IDataStoreBucketsContainer<Item>> dataStoreContainers = new List<IDataStoreBucketsContainer<Item>>();

        static void Main(string[] args)
        {
            // Test query conditions - hardcoded for the test
            const int start = 15000;
            const int num = 25;

            Console.WriteLine($"Data Stores Lists Aggregator\n\nstart: {start}, num: {num}\n");

            // Data preparation
            if (!Init())
            {
                PressAnyKeyToQuit();
                return;
            }

            // Optional - Evaluate buckets size
            var lstTuple = new List<Tuple<IList<int>, float, IList<float>>>();
            for (int i = 0; i < dataStoreContainers.Count; i++)
                lstTuple.Add((dataStoreContainers[i] as DataStoreBucketsContainer<Item>).GetInfo());

            // Create test query
            var query = new Query { Filter = $"{start}", Length = num };

            Item itemToRemove = null;

            for (int i = 0; i < 3; i++)
            {
                switch (i)
                {
                    case 1:
                        // Item will be added to the 2nd Data Store container (Data Store = 1)
                        int dataStore = 1;
                        (dataStoreContainers[dataStore] as DataStoreBucketsContainer<Item>).AddItem(
                                    new Item { Word = $"{itemToRemove.Word}__***", Num = -start / 10, DataStore = dataStore });
                        break;

                    case 2:
                        // Item removal
                        foreach (var daraStoreContainer in dataStoreContainers)
                            (daraStoreContainer as DataStoreBucketsContainer<Item>).RemoveItem(itemToRemove);
                        break;
                }

                // Test Call
                var items = Aggregator.GetPage(dataStoreContainers, new ComparerItem(), query);

                // The 2nd item (#1) from result list is to be removed
                itemToRemove = items[1];

                Console.WriteLine("Combined Serial Num.   Num. in Page  Data Store   Word".ToUpper());
                int j = 0;
                foreach (var item in items)
                    Console.WriteLine($"     {item.Num}                  {j++}            {item.DataStore}        {item.Word}");

                Console.WriteLine();
            }

            PressAnyKeyToQuit();
        }

        // Method for data preparation
        static bool Init()
        {
            string currentDir = Directory.GetCurrentDirectory();
            var path = Path.Combine(currentDir, textFileName);
            if (!File.Exists(path))
                path = Path.Combine(currentDir, @"..\..\", textFileName);

            if (!File.Exists(path))
            {
                Console.WriteLine($"\nERROR: Test file \"{textFileName}\" not found.");
                return false;
            }

            var lst = new List<string>();
            lst.AddRange(File.ReadAllText(path).ToLower() // all letters are converted to lower ones
                                // The following chars are removed from text and text split by blanks to words
                                .Replace(".", "")
                                .Replace(",", "")
                                .Replace("\n", "")
                                .Replace("\r", "")
                                .Replace("\"", "")
                                .Replace("'", "")
                                .Replace("!", "")
                                .Replace("?", "")
                                .Replace("(", "")
                                .Replace(")", "")
                                .Replace("*", "")
                                .Replace("_", "")
                                .Replace("-", "")
                                .Replace("[", "")
                                .Replace("]", "")
                                .Replace("&", "")
                                .Replace(">", "")
                                .Replace("<", "")
                                .Replace("0", "")
                                .Replace("1", "")
                                .Replace("2", "")
                                .Replace("3", "")
                                .Replace("4", "")
                                .Replace("5", "")
                                .Replace("6", "")
                                .Replace("7", "")
                                .Replace("8", "")
                                .Replace("9", "")
                                .Replace("{", "")
                                .Replace("}", "")
                                .Replace("~", "")
                                .Replace(":", "")
                                .Replace(";", "")
                                .Replace("\\", "")
                                .Split(' '));
            
            // Only distinct words starting with letter are considered
            var words = lst.Distinct().Where(w => !string.IsNullOrEmpty(w) && w[0] >= 'a' && w[0] <= 'z').ToArray();

            // Data Store containers: Dictionary<dataStore, list of items>
            var dctDataStores = new Dictionary<int, List<Item>>();

            // List of all words
            var lstWords = new List<Item>();

            // Create items and distribute them among Data Store containers
            for (int i = 0; i < words.Length; i++)
            {
                var item = new Item { Word = words[i] };
                lstWords.Add(item);

                int keyDataStore = i % numOfDataStores;
                if (!dctDataStores.ContainsKey(keyDataStore))
                    dctDataStores[keyDataStore] = new List<Item>();

                item.DataStore = keyDataStore;
                dctDataStores[keyDataStore].Add(item);
            }

            // Distribute items by buckets in each Data Store container
            for (int i = 0; i < numOfDataStores; i++)
                dataStoreContainers.Add(new DataStoreBucketsContainer<Item>(dctDataStores[i], item => ComparerItem.DetermineItemBucket(item)));

            // Sort the entire array in lexigraphics order of "Str" members and
            //    assign set item's serial number to "Num" member.
            int k = 0;
            var sortedTotalList = lstWords.OrderBy(item => item.Word).ToList();
            foreach (var item in sortedTotalList)
                item.Num = k++;

            var theLast = sortedTotalList.Last();

            return true;
        }

        static void PressAnyKeyToQuit()
        {
            Console.WriteLine("\nPress any key to quit...");
            Console.ReadKey();
        }
    }

    //#region Proxy class for buckets container on each Data Store

    //// Stub for proxy class
    //public class DataStoreBucketsContainerProxy<T> : IDataStoreBucketsContainer<T>
    //{
    //    // Call appropriate method or real class remotely
    //    public Tuple<Dictionary<int, int>, int> GetBucketsLength()
    //    {
    //        return null;
    //    }

    //    // Call appropriate method or real class remotely
    //    public Dictionary<int, List<T>> GetBucketsContent(int beginBucket, int endBucket)
    //    {
    //        return null;
    //    }
    //}

    //#endregion // Proxy class for buckets container on each Data Store
}
