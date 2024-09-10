using System.Collections.Concurrent;
using System.Diagnostics;

namespace map_bench_dot_net;

using KeyType = string;
using ValueType = ulong;

public struct Measurement
{
    /// <summary>
    ///     A total number of operations.
    /// </summary>
    public long TotalOps { get; set; }

    /// <summary>
    ///     An average value of latency.
    /// </summary>
    public long Latency { get; set; }

    /// <summary>
    ///     A total number of threads.
    /// </summary>
    public long ThreadCount { get; set; }
}

public struct Mix
{
    /// <summary>
    ///     The percentage of operations in the mix that are reads.
    /// </summary>
    public byte Read { get; set; }

    /// <summary>
    ///     The percentage of operations in the mix that are inserts.
    /// </summary>
    public byte Insert { get; set; }

    /// <summary>
    ///     The percentage of operations in the mix that are removals.
    /// </summary>
    public byte Remove { get; set; }

    /// <summary>
    ///     The percentage of operations in the mix that are updates.
    /// </summary>
    public byte Update { get; set; }

    /// <summary>
    ///     The percentage of operations in the mix that are update-or-inserts.
    /// </summary>
    public byte Upsert { get; set; }

    /// <summary>
    ///     Constructs a very read-heavy workload (~95%), with limited concurrent modifications.
    /// </summary>
    public static Mix ReadHeavy()
    {
        return new Mix
        {
            Read = 95,
            Insert = 2,
            Update = 1,
            Remove = 1,
            Upsert = 1
        };
    }

    /// <summary>
    ///     Constructs a read-only workload.
    /// </summary>
    public static Mix ReadOnly()
    {
        return new Mix
        {
            Read = 100,
            Insert = 0,
            Update = 0,
            Remove = 0,
            Upsert = 0
        };
    }

    internal static void Shuffle(List<Operation> opMix)
    {
        var rng = new Random();
        var n = opMix.Count;

        while (n > 1)
        {
            n--;
            var k = rng.Next(n + 1);
            (opMix[k], opMix[n]) = (opMix[n], opMix[k]);
        }
    }

    public List<Operation> ToOps()
    {
        var list = new List<Operation>(100);
        list.AddRange(Enumerable.Repeat(Operation.Read, Read));
        list.AddRange(Enumerable.Repeat(Operation.Insert, Insert));
        list.AddRange(Enumerable.Repeat(Operation.Remove, Remove));
        list.AddRange(Enumerable.Repeat(Operation.Update, Update));
        list.AddRange(Enumerable.Repeat(Operation.Upsert, Upsert));
        Shuffle(list);

        return list;
    }
}

public enum Operation
{
    Read,
    Insert,
    Remove,
    Update,
    Upsert
}

internal class Bench
{
    private static int RunOps(ConcurrentDictionary<KeyType, ValueType> dict, Keys keys, List<Operation> opMix, int opsPerThread, int keysPerThread)
    {
        var random = new Random(Thread.CurrentThread.ManagedThreadId);
        var opMixCount = opMix.Count;
        var totalSuccess = 0;
        var newKeys = keys.Alloc(keysPerThread).GetEnumerator();

        // Main loop
        for (var i = 0; i < opsPerThread; i++)
        {
            var op = opMix[i % opMixCount];
            var r = random.Next();
            var success = false;

            switch (op)
            {
                case Operation.Read:
                {
                    success = dict.TryGetValue(keys.Random(r), out var existingValue);
                    break;
                }

                case Operation.Insert:
                {
                    newKeys.MoveNext();
                    success = dict.TryAdd(newKeys.Current, 0UL);
                    break;
                }

                case Operation.Remove:
                {
                    success = dict.TryRemove(keys.Random(r), out var removedVal);
                    break;
                }

                case Operation.Update:
                {
                    success = dict.TryGetValue(keys.Random(r), out var existingValue) &&
                              dict.TryUpdate(keys.Random(r), existingValue + 1, existingValue);
                    break;
                }

                case Operation.Upsert:
                {
                    success = dict.AddOrUpdate(keys.Random(r), 1, (k, oldValue) => oldValue + 1) == 1;
                    break;
                }
            }

            totalSuccess += success ? 0 : 1;
        }

        return totalSuccess;
    }

    public static Measurement RunWorkload(List<Operation> operations, RunConfig config, Keys keys, int keysPerThread)
    {
        var numThreads = config.Threads;
        var dict = new ConcurrentDictionary<KeyType, ValueType>(numThreads, config.InitialCapacity);

        Console.WriteLine($"start {numThreads} threads");

        keys.Reset();
        foreach (var k in keys.Alloc(config.Prefill)) dict.TryAdd(k, 0UL);

        var barrier = new Barrier(numThreads + 1);
        var tasks = new List<Task>();
        var opsPerThread = config.TotalOps / numThreads;
        var elapsedMilliseconds = new ConcurrentBag<long>();
        var realTotalOps = opsPerThread * numThreads;

        for (var i = 0; i < numThreads; i++)
            tasks.Add(Task.Run(() =>
            {
                barrier.SignalAndWait();
                var start = Stopwatch.StartNew();
                RunOps(dict, keys, operations, opsPerThread, keysPerThread);
                start.Stop();
                elapsedMilliseconds.Add(start.ElapsedMilliseconds);
            }));

        barrier.SignalAndWait();
        Task.WaitAll(tasks.ToArray());

        var totalMilliseconds = elapsedMilliseconds.Sum();
        var avgLatency = (long)totalMilliseconds * 1_000_000 / realTotalOps;
        Console.WriteLine(
            $"config complete in {totalMilliseconds} milliseconds (ops: {realTotalOps}, avg: {avgLatency} ns)");

        return new Measurement
        {
            TotalOps = realTotalOps,
            Latency = avgLatency,
            ThreadCount = numThreads
        };
    }

    

    internal struct RunConfig
    {
        public int Threads { get; set; }
        public int InitialCapacity { get; set; }
        public int TotalOps { get; set; }
        public int Prefill { get; set; }
    }

    internal class Keys
    {
        private int _allocated;
        private readonly KeyType[] _keys;

        public Keys(int totalKeys)
        {
            var random = new Random();
            var uniqueSet = new HashSet<KeyType>();
            while (uniqueSet.Count < totalKeys) uniqueSet.Add((KeyType)random.NextInt64().ToString());
            _keys = uniqueSet.ToArray();
        }

        internal void Reset()
        {
            _allocated = 0;
        }

        public KeyType Random(int i)
        {
            return _keys[i % _allocated];
        }

        public IEnumerable<KeyType> Alloc(int count)
        {
            var startIndex = Interlocked.Add(ref _allocated, count) - count;
            return _keys.Skip(startIndex).Take(count);
        }
    }
}