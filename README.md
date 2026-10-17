# Sorted Page from Distributed Unsorted Data Sets

**Author:** Igor Ladnik ([email](mailto:ladnik@hotmail.com)).

This is a .NET 10 modernization of the original bucket-based paging example. The [original HTML article](docs/Sorted%20Page%20from%20Unsorted%20Distributed%20Data.html) and [diagram](docs/MergeForPage.png) remain historical references; their .NET Framework build details, page-bound formula, and description of the current API are outdated. The original article declares **CPOL**. This repository does not replace or invent a license declaration.

## Build and run

Install the .NET 10 SDK and a Visual Studio release that supports .NET 10 (Visual Studio 2026 or newer). Both projects use stable C# 14 with nullable reference analysis enabled. From the repository root:

```powershell
dotnet restore MergeForPage.sln
dotnet build MergeForPage.sln -c Release --no-restore
dotnet TestApp/bin/Release/net10.0/TestApp.dll
dotnet TestApp/bin/Release/net10.0/TestApp.dll "C:\data\my-text.txt"
```

The last command is optional. It reads the supplied text file; a missing path or a file without words produces an error and nonzero exit code. The default run uses `TestApp/t8.shakespeare.txt`, copied to the output directory. This file came from the original author's archived sample zip in Dropbox. Its header identifies a World Library copyrighted electronic edition and restricts redistribution to personal, noncommercial use; retain and review that notice before redistributing the data. The demo resolves it from the application directory, independent of the working directory. It removes the original sample's punctuation and digits, splits on spaces, and uses distinct lowercase words beginning with ASCII letters. It tests an offset of 15000 when the input is large enough. The demo queries a page spanning multiple buckets, inserts and removes an item, prints each result page with global index, page index, store, and word, and checks each page against a full-sort baseline. It exits nonzero on failure and does not wait for keyboard input.

Repository layout:

- `AggregatedSortedPage/`: generic bucket container, interface, and aggregator.
- `TestApp/`: console demonstration and original Shakespeare sample.
- `docs/`: historical article and diagram.
- `MergeForPage.sln`: Visual Studio solution.

## API and ordering

`Aggregator.GetPage` accepts a **list** of `IDataStoreBucketsContainer<T>` stores, a comparer, and a `Query`. `Query.Filter` is a zero-based, nonnegative decimal offset (within `Int32` range); `Query.Length` is a positive `Int32` page length. The result equals a full sort of all included items followed by `Skip(offset).Take(length)`. An offset at or beyond the total returns an empty array. Other filter expressions are unsupported and throw; they are not search queries. Null arguments, null store entries, nonnumeric or negative offsets, and nonpositive lengths throw argument exceptions.

```csharp
var stores = new List<IDataStoreBucketsContainer<string>>
{
    new DataStoreBucketsContainer<string>(new[] { "apple", "pear" },
        word => word[0] - 'a')
};
var page = Aggregator.GetPage(stores, StringComparer.Ordinal,
    new Query { Filter = "0", Length = 2 });
```

**Bucket-order invariant:** for any two included items `a` and `b`, if `Bucket(a) < Bucket(b)`, then `comparer.Compare(a, b) <= 0`. Merely using identical bucket rules in every store is insufficient. The demonstration uses lowercase ASCII words and ordinal comparison, so its first-letter and split-letter buckets obey this invariant. Negative bucket keys exclude items at insertion. Duplicates are retained by the library. Equal comparer results do not guarantee the same relative order across separate queries; stable pagination requires a deterministic tie-breaker that also respects bucket ordering.

The query first obtains each store's bucket counts. If the offset is within the data, it finds the first bucket whose cumulative count exceeds the offset. For a nonempty page, the last requested existing index is `min(totalCount, offset + length) - 1`; the ending bucket is the bucket containing that index. Only buckets between these keys are fetched, sorted within each bucket, and concatenated by ascending bucket key. Empty pages require no content transfer. Internal count and bound arithmetic uses `long`; each store's existing interface still reports `int` counts, and the public offset and length remain `int`.

With `S` stores, `B` distinct buckets, and `M` fetched items, the count phase transfers bucket counts from every store; bucket selection sorts up to `B` keys, and sorting fetched items costs up to `O(M log M)`. Content transfer is `O(M)`, not necessarily page-sized: one large bucket can still transfer many items. This sample has no remote-call proxy or distributed transaction infrastructure.

## Concurrency and mutable items

`GetBucketsLength` and `GetBucketsContent` each take a per-store lock. Content returns independent list copies made under that lock. These separate calls **do not** form a snapshot across the two query phases or across stores. If updates overlap a query, the caller must coordinate all participating stores for the whole query. Remote coordination is the caller's responsibility.

For the supplied in-process stores, use `DataStoreBucketsContainer<T>.WithAllLocked`. It takes dedicated `System.Threading.Lock` instances in a fixed creation order, holds them through the callback, and releases them in reverse order. `AddItem` and `RemoveItem` take the same locks:

```csharp
var stores = new[] { storeA, storeB }; // DataStoreBucketsContainer<string>
IList<IDataStoreBucketsContainer<string>> proxies =
    stores.Cast<IDataStoreBucketsContainer<string>>().ToList();
var page = DataStoreBucketsContainer<string>.WithAllLocked(stores,
    () => Aggregator.GetPage(proxies, StringComparer.Ordinal,
        new Query { Filter = "0", Length = 20 }));
```

Every included in-process store must participate. Avoid unrelated blocking work inside the callback. A copied bucket list does not clone mutable `T` objects: changing an object's sort key or bucket assignment while stored is unsupported. Remove and reinsert it for such changes, and coordinate that operation with queries. The method cannot protect against uncoordinated mutation of an object's properties.
