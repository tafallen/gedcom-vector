using System;

namespace Gedcom.Vector.Parsing;

/// <summary>
/// A fast span-based string pool for deduplicating repeated string instances
/// (tags, xrefs, dates, places, surnames, given names) without allocating on pool hits.
/// </summary>
public sealed class GedcomStringPool
{
    private struct Entry
    {
        public int HashCode;
        public int Next;
        public string Value;
    }

    private int[] _buckets;
    private Entry[] _entries;
    private int _count;

    /// <summary>
    /// Initializes a new instance of the <see cref="GedcomStringPool"/> class.
    /// </summary>
    /// <param name="capacity">Initial pool capacity.</param>
    public GedcomStringPool(int capacity = 256)
    {
        int size = GetPrime(capacity);
        _buckets = new int[size];
        Array.Fill(_buckets, -1);
        _entries = new Entry[size];
    }

    /// <summary>
    /// Gets an interned string for the provided character span.
    /// </summary>
    public string GetOrAdd(ReadOnlySpan<char> span)
    {
        if (span.IsEmpty)
        {
            return string.Empty;
        }

        int hashCode = string.GetHashCode(span, StringComparison.Ordinal);
        uint uHashCode = (uint)hashCode;
        int bucket = (int)(uHashCode % (uint)_buckets.Length);

        for (int i = _buckets[bucket]; i >= 0; i = _entries[i].Next)
        {
            if (_entries[i].HashCode == hashCode && span.Equals(_entries[i].Value.AsSpan(), StringComparison.Ordinal))
            {
                return _entries[i].Value;
            }
        }

        string s = span.ToString();
        if (_count == _entries.Length)
        {
            Resize();
            bucket = (int)(uHashCode % (uint)_buckets.Length);
        }

        int index = _count++;
        _entries[index].HashCode = hashCode;
        _entries[index].Next = _buckets[bucket];
        _entries[index].Value = s;
        _buckets[bucket] = index;

        return s;
    }

    /// <summary>
    /// Gets an interned string for the provided string, or null if the string is null.
    /// </summary>
    public string? GetOrAdd(string? s)
    {
        if (s == null) return null;
        return GetOrAdd(s.AsSpan());
    }

    private void Resize()
    {
        int newSize = GetPrime(_entries.Length * 2);
        int[] newBuckets = new int[newSize];
        Array.Fill(newBuckets, -1);
        Entry[] newEntries = new Entry[newSize];

        Array.Copy(_entries, newEntries, _count);

        for (int i = 0; i < _count; i++)
        {
            int bucket = (int)((uint)newEntries[i].HashCode % (uint)newSize);
            newEntries[i].Next = newBuckets[bucket];
            newBuckets[bucket] = i;
        }

        _buckets = newBuckets;
        _entries = newEntries;
    }

    /// <summary>
    /// Resets and clears all interned strings from the pool, allowing memory recycling.
    /// </summary>
    public void Clear()
    {
        if (_count > 0)
        {
            Array.Fill(_buckets, -1);
            Array.Clear(_entries, 0, _count);
            _count = 0;
        }
    }

    /// <summary>
    /// Gets the current number of unique strings stored in the pool.
    /// </summary>
    public int Count => _count;

    private static int GetPrime(int min)
    {
        int[] primes = { 37, 67, 131, 257, 521, 1031, 2053, 4099, 8209, 16411, 32771, 65537, 131101, 262147 };
        foreach (int prime in primes)
        {
            if (prime >= min) return prime;
        }
        return min | 1;
    }
}
