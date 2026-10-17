using System;
using System.Collections.Generic;

namespace MergeForPaging;

public class Item
{
    public string Word { get; set; } = string.Empty;
    public object? Payload { get; set; }
    public int DataStore { get; set; }
    public long Num { get; set; }
}

public class ComparerItem : IComparer<Item>
{
    public int Compare(Item? x, Item? y) =>
        string.Compare(x?.Word, y?.Word, StringComparison.Ordinal);

    // The buckets follow ordinal order for lowercase ASCII words.
    public static int DetermineItemBucket(Item? item)
    {
        if (item?.Word is not { Length: > 0 } word || word[0] is < 'a' or > 'z')
            return -1;

        return word[0] switch
        {
            >= 'i' and <= 'k' => BaseBucket('i'),
            >= 'n' and <= 'o' => BaseBucket('n'),
            >= 'q' and <= 'r' => BaseBucket('q'),
            >= 'u' and <= 'v' => BaseBucket('u'),
            >= 'w' => BaseBucket('w'),
            'c' or 'p' or 's' => SplitBucket(word),
            var first => BaseBucket(first)
        };
    }

    private static int BaseBucket(char first) => (first - 'a') * 10;

    private static int SplitBucket(string word) =>
        BaseBucket(word[0]) + (word.Length > 1 && word[1] > 'm' ? 1 : 0);
}
