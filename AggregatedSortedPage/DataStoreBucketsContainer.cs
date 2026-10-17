using System;
using System.Collections.Generic;
using System.Linq;

namespace AggregatedSortedPage
{
    #region Interface for buckets container on each Data Store

    // Interface implemented by real class containing items
    //    and by its proxy running in Aggregator
    public interface IDataStoreBucketsContainer<T>
    {
        Tuple<Dictionary<int, int>, int> GetBucketsLength();
        Dictionary<int, List<T>> GetBucketsContent(int beginBucket, int endBucket);
    }

    #endregion // Interface for buckets container on each Data Store

    #region Class for buckets container on each Data Store

    // Items container for a Data Store
    public class DataStoreBucketsContainer<T> : IDataStoreBucketsContainer<T>
    {
        // Bucket distribution
        private IDictionary<int, List<T>> dctBucket = new Dictionary<int, List<T>>();

        private object locker = new object();

        // Determine bucket for give item
        private Func<T, int> dlgtDetermineItemBucket;
        
        // Ctor
        public DataStoreBucketsContainer(IEnumerable<T> items, Func<T, int> dlgtDetermineItemBucket)
        {
            this.dlgtDetermineItemBucket = dlgtDetermineItemBucket;

            if (items != null)
                foreach (var item in items)
                    PlaceItemToBucket(item);
        }

        #region IDataStoreBucketsContainer<T> Methods

        // Method of the interface. Return number of item in each bucket and
        //    total number of items in the Data Store
        public Tuple<Dictionary<int, int>, int> GetBucketsLength()
        {
            int total = 0;
            var dctBucketCounts = new Dictionary<int, int>();

            lock (locker)
            {
                foreach (var key in dctBucket.Keys)
                {
                    int n = dctBucket[key].Count;

                    if (!dctBucketCounts.ContainsKey(key))
                        dctBucketCounts[key] = 0;

                    dctBucketCounts[key] += n;

                    total += n;
                }
            }

            return new Tuple<Dictionary<int, int>, int>(dctBucketCounts, total);
        }

        // Method of the interface. Return lists for bucket range
        //    between "beginBucket" and "endBucket"
        public Dictionary<int, List<T>> GetBucketsContent(int beginBucket, int endBucket)
        {
            var dct = new Dictionary<int, List<T>>();

            lock (locker)
            {
                foreach (var k in dctBucket.Keys.Where(key => key >= beginBucket && key <= endBucket))
                    dct[k] = dctBucket[k];
            }

            return dct;
        }

        #endregion // IDataStoreBucketsContainer<T> Methods

        #region Public Add / Renmove item

        public void AddItem(T item)
        {
            lock (locker)
                PlaceItemToBucket(item);
        }

        public void RemoveItem(T item)
        {
            lock (locker)
                RemoveItemFromBucket(item);
        }

        #endregion // Public Add / Renmove item

        #region Aux. - Buckets info

        public Tuple<IList<int>, float, IList<float>> GetInfo()
        {
            var tuple = GetBucketsLength();
            float avr = (float)tuple.Item2 / tuple.Item1.Keys.Count();

            var lstSize = new List<int>();
            var lstOverAvr = new List<float>();
            foreach (var key in tuple.Item1.Keys.OrderBy(key => key))
            {
                int n = tuple.Item1[key];
                lstSize.Add(n);
                lstOverAvr.Add(n / avr);
            }
                         
            return new Tuple<IList<int>, float, IList<float>>(lstSize, avr, lstOverAvr);
        }

        #endregion // Aux. - Buckets info

        #region Private Methods

        private void PlaceItemToBucket(T item)
        {
            int bucket = dlgtDetermineItemBucket(item);
            if (bucket > -1)
            {
                if (!dctBucket.ContainsKey(bucket))
                    dctBucket[bucket] = new List<T>();

                dctBucket[bucket].Add(item);
            }
        }

        private void RemoveItemFromBucket(T item)
        {
            int bucket = dlgtDetermineItemBucket(item);
            if (bucket > -1)
                dctBucket[bucket].Remove(item);
        }

        #endregion // Private Methods
    }

    #endregion // Class for buckets container on each Data Store
}
