using map_bench_dot_net;
using System.Runtime;

var read99 = Mix.Read99();
var max99 = read99.ToOps();
var capacity = 1_000_000;
var totalOps = capacity * 55;
var prefill = capacity / 2;
var keys_needed_for_inserts = (totalOps * read99.Insert / 100) + 1000;
var totalKeys = prefill + keys_needed_for_inserts + 1000; // 1000 needed for some rounding error?

List<Measurement> measurements99 = new();
List<Measurement> measurements100 = new();
var keys = new Bench.Keys(totalKeys);

for (var numThreads = 0; numThreads < Environment.ProcessorCount; numThreads++)
{
    var config = new Bench.RunConfig
    {
        Threads = numThreads + 1,
        InitialCapacity = capacity,
        TotalOps = totalOps,
        Prefill = prefill
    };

    Console.Write($" 99% read {numThreads,2} threads ... ");
    var keysPerThread = keys_needed_for_inserts / config.Threads;
    var measurement = Bench.RunWorkload(max99, config, keys, keysPerThread);
    measurements99.Add(measurement);
    Console.WriteLine($"avg: {measurement.AvLatency} ns");
}

var read100 = Mix.ReadOnly();
var mix100 = read100.ToOps();

for (var numThreads = 0; numThreads < Environment.ProcessorCount; numThreads++)
{
    var config = new Bench.RunConfig
    {
        Threads = numThreads + 1,
        InitialCapacity = capacity,
        TotalOps = totalOps,
        Prefill = prefill
    };

    Console.Write($"100% read {numThreads,2} threads ... ");
    var keysPerThread = keys_needed_for_inserts / config.Threads;
    var measurement = Bench.RunWorkload(mix100, config, keys, keysPerThread);
    measurements100.Add(measurement);
    Console.WriteLine($"avg: {measurement.AvLatency} ns");
}

var rustFile = "perf_dotnet_data.rs";

using (var writer = new StreamWriter(rustFile))
{
    writer.WriteLine("use std::sync::LazyLock;");
    writer.WriteLine("use crate::perf::Measurement;");
    writer.WriteLine("");
    writer.WriteLine("pub static PERF_DATA_DOT_NET_99: LazyLock<Vec<Measurement>> = LazyLock::new(|| {  vec![");

    foreach (var m in measurements99)
    {
        var code_line = string.Format("Measurement {{ name: \"c#\", thread_count: {0}, latency: {1} }},",
            m.ThreadCount,
            m.AvLatency
        );

        writer.WriteLine(code_line);
    }

    writer.WriteLine("] });");

    writer.WriteLine("pub static PERF_DATA_DOT_NET_100: LazyLock<Vec<Measurement>> = LazyLock::new(|| {  vec![");

    foreach (var m in measurements100)
    {
        var code_line = string.Format("Measurement {{ name: \"c#\", thread_count: {0}, latency: {1} }},",
            m.ThreadCount,
            m.AvLatency
        );

        writer.WriteLine(code_line);
    }

    writer.WriteLine("] });");
}

Console.WriteLine("Results file written successfully!");