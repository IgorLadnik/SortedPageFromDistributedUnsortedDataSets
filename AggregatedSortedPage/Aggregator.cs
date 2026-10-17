using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AggregatedSortedPage
{
    public static class Aggregator
    {
        #region Public Aggregation Methods

        // Get Page Method
        public static T[] GetPage<T>(IList<IDataStoreBucketsContainer<T>> dataStoreProxies, IComparer<T> comparerT, Query query)
        {
            if (query == null || string.IsNullOrEmpty(query.Filter) || query.Length < 1)
                return Empty<T>();

            Tuple<int, int, int> tuple = null;

            int num = query.Length;
            int start = -1;
            if (int.TryParse(query.Filter, out start) && start > -1)
                // Process query of type (start, num)
                tuple = GetBeginEndOffset(dataStoreProxies, start, num);
            else
                // Process different types of query - not implemented yet
                tuple = GetBeginEndOffset(dataStoreProxies, string.Empty, num);

            return tuple != null && tuple.Item1 > 0 && tuple.Item2 > 0 && tuple.Item3 > 0
                        ? GetResult(dataStoreProxies, comparerT, 
                                            beginBucket: tuple.Item1, 
                                            endBucket:   tuple.Item2, 
                                            offset:      tuple.Item3, 
                                            num: num) 
                        : Empty<T>();
        }

        #endregion // Public Aggregation Methods

        #region Private Methods

        // Method for query (start, num).
        private static Tuple<int, int, int> GetBeginEndOffset<T>(IList<IDataStoreBucketsContainer<T>> dadaStoreProxies, int start, int num)
        {
            int numOfDataStored = dadaStoreProxies.Count();

            // Tuple<Dictionary<int, int>, int> - for each Data Store
            //    (bucket -> num. of items in this bucket)
            //    total num. of items in all buckets.
            var callbacksGetBucketLengths = new Func<Tuple<Dictionary<int, int>, int>>[numOfDataStored];
            for (int i = 0; i < numOfDataStored; i++)
                callbacksGetBucketLengths[i] = dadaStoreProxies[i].GetBucketsLength;

            // Parallel call for all Data Stores to get number of items in each bucket and 
            //    total nuber of items in each Data Store.
            var lstTuple = GetAggregatedList(callbacksGetBucketLengths);

            // Calculate combined number of items for each bucket in all Data Stores "dctBucketCounts"
            //    total number of items n all Data Stores.
            var dctBucketCounts = new Dictionary<int, int>();
            var arrTuple = lstTuple.ToArray();
            int total = 0;
            for (int i = 0; i < dadaStoreProxies.Count(); i++)
            {
                var tuple = arrTuple[i];
                total += tuple.Item2;

                var dct = tuple.Item1;
                foreach (var key in dct.Keys)
                {
                    if (!dctBucketCounts.ContainsKey(key))
                        dctBucketCounts[key] = 0;

                    dctBucketCounts[key] += dct[key];
                }
            }

            // Determine required buckets range according to query "beginBucket" and "endBucket".
            //    "offset" indicates number of slipped items after final sorting and aggregation.
            int prevCount = 0;
            int count = 0;
            int offset = 0;
            int numOfBuckets = dctBucketCounts.Keys.Count;
            var orderedKeys = dctBucketCounts.Keys.OrderBy(k => k).ToArray();
            int begin = -1;
            int end = -1;
            for (int i = 0; i < orderedKeys.Length; i++)
            {
                prevCount = count;
                count += dctBucketCounts[orderedKeys[i]];

                if (count >= start && begin == -1)
                {
                    begin = i;
                    offset = start - prevCount;
                }

                if (count >= start + num && end == -1)
                {
                    end = i;
                    break;
                }
            }

            if (begin >= 0)
            {
                if (end == -1)
                    end = orderedKeys.Length - 1;
            }
            else
                return null;

            return new Tuple<int, int, int>(/*beginBucket*/ orderedKeys[begin], /*endBucket*/ orderedKeys[end], offset);
        }

        // Method for query (filterRule, num) - not implemented yet
        private static Tuple<int, int, int> GetBeginEndOffset<T>(
                    IList<IDataStoreBucketsContainer<T>> dataStoreProxies, string filterRule, int num)
        {
            return new Tuple<int, int, int>(-1, -1, -1);
        }

        private static T[] GetResult<T>(IList<IDataStoreBucketsContainer<T>> dataStoreProxies, IComparer<T> comparerT, 
                                        int beginBucket, int endBucket, int offset, int num)
        {
            int numOfDataStores = dataStoreProxies.Count();

            var callbacksGetBucketsContent = new Func<int, int, Dictionary<int, List<T>>>[numOfDataStores];
            for (int i = 0; i < numOfDataStores; i++)
                callbacksGetBucketsContent[i] = dataStoreProxies[i].GetBucketsContent;

            // Parallel call for all Data Stores to get content of buckets between "beginBucket" and  "endBucket".
            var lstView = GetAggregatedList(null, callbacksGetBucketsContent, beginBucket, endBucket);

            // Aggregate content of selected bucket from all Data Stores.
            var dctItem = new Dictionary<int, List<T>>(); // bucket -> aggregated content from all Data Stores
            foreach (var dct in lstView)
                foreach (var key in dct.Keys)
                {
                    if (!dctItem.ContainsKey(key))
                        dctItem[key] = new List<T>();

                    dctItem[key].AddRange(dct[key]);
                }

            // Final data aggeregation: first sort in each bucket and then finally unite their content. 
            var lstItem = new List<T>();
            Parallel.ForEach(dctItem.Keys.OrderBy(key => key),
                             key => lstItem.AddRange(dctItem[key].OrderBy(item => item, comparerT)));

            // Cut out required page from sorted list.
            return SubArray(lstItem.ToArray(), offset, num);
        }

        private static IList<T> GetAggregatedList<T>(Func<T>[] callbacks, 
                                                     Func<int, int, T>[] callbacksWithParams = null,
                                                            int beginBucket = -1, int endBucket = -1)
        {
            int numOfDataSores = callbacksWithParams == null ? callbacks.Length : callbacksWithParams.Length;
            var lsts = new List<T>[numOfDataSores];
            for (int i = 0; i < numOfDataSores; i++)
                lsts[i] = new List<T>();

            Parallel.For(0, numOfDataSores, i => lsts[i].Add(callbacksWithParams == null
                                ? callbacks[i]()
                                : callbacksWithParams[i](beginBucket, endBucket)));

            var lstRet = new List<T>();
            for (int i = 0; i < numOfDataSores; i++)
                lstRet.AddRange(lsts[i]);

            return lstRet;
        }

        private static T[] SubArray<T>(IList<T> data, int offset, int length)
        {
            if (data.Count - offset <= 0)
                return new T[0];

            int a = data.Count - offset;
            return data.Skip(offset).Take(a > length ? length : a).ToArray();
        }

        private static T[] Empty<T>() { return new T[0]; }

        #endregion // Private Methods
    }

    public class Query
    {
        public string Filter { get; set; }
        public int Length { get; set; }
    }
}
