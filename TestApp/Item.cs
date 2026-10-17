using System;
using System.Collections.Generic;

namespace MergeForPaging
{
    // Lists of this type to be sorted and merged accross several Data Stores.
    public class Item
    {
        // Member for sorting
        public string Word { get; set; }

        public object Payload { get; set; }

        #region Optional members used for testing only 

        // DataStore of the item
        public int DataStore { get; set; }

        // Serial number of the item in combined sorted list
        public long Num { get; set; }

        #endregion // Optional members used for testing only 
    }

    #region Comparer class for item

    //                 BUCKETS
    // Serial Number  Key   1st Letter  2nd Letter
    //    [0]	        0	   a
    //    [1]	       10	   b
    //    [2]	       20	   c         a - m
    //    [3]	       21	   c         n - z
    //    [4]	       30	   d
    //    [5]	       40	   e
    //    [6]	       50	   f
    //    [7]	       60	   g
    //    [8]	       70	   h
    //    [9]	       80	   i, j, k
    //    [10]	      110	   l
    //    [11]	      120	   m
    //    [12]	      130	   n, o
    //    [13]	      150	   p          a - m
    //    [14]	      151	   p          n - z
    //    [15]	      160	   q, r
    //    [16]	      180	   s          a - m
    //    [17]	      181	   s          n - z
    //    [18]	      190	   t
    //    [19]	      200	   u, v
    //    [20]	      220	   w, x, y, z

    public class ComparerItem : IComparer<Item>
    {
        // Method of "IComparer" interface 
        public int Compare(Item x, Item y)
        {
            return string.Compare(x.Word, y.Word, StringComparison.CurrentCulture);
        }

        // Hardcoded rules to determine bucket of "item"
        public static int DetermineItemBucket(Item item)
        {
            if (item == null || string.IsNullOrEmpty(item.Word))
                return -1;

            char ch0 = item.Word[0];

            if (ch0 >= 'a' && ch0 <= 'z')
            {
                if (ch0 >= 'i' && ch0 <= 'k')                  // bucket "i, j, k"
                    return RetBucket('i');

                if (ch0 >= 'n' && ch0 <= 'o')                  // bucket "n, o"
                    return RetBucket('n');

                if (ch0 >= 'q' && ch0 <= 'r')                  // bucket "q, r"
                    return RetBucket('q');

                if (ch0 >= 'u' && ch0 <= 'v')                  // bucket "u, v"
                    return RetBucket('u');

                if (ch0 >= 'w')                                // bucket "w <= ..."
                    return RetBucket('w');

                int ret = -1;
                if ((ret = SplitBucket('c', item.Word)) != -1)  // buckets "c - cm", "cm < ..." 
                    return ret;

                if ((ret = SplitBucket('p', item.Word)) != -1)  // buckets "p - pm", "pm < ..."
                    return ret;

                if ((ret = SplitBucket('s', item.Word)) != -1)  // buckets "s - sm", "sm < ..."
                    return ret;

                return RetBucket(ch0);
            }

            return -1;
        }

        private static int RetBucket(char ch)
        {
            return (ch - 'a') * 10;
        }

        private static int SplitBucket(char bch, string s)
        {
            if (s[0] != bch)
                return -1;

            return s.Length > 1 && s[1] > 'm' ? RetBucket(bch) + 1 : RetBucket(bch);
        }
    }

    #endregion Comparer class for item
}
