using map_bench_dot_net;
using System.Runtime;

var spec = Mix.Read99();
var mix = spec.ToOps();
var capacity = 1_000_000;
var totalOps = capacity * 55;
var prefill = capacity / 2;
var keys_needed_for_inserts = (totalOps * spec.Insert / 100) + 1000;
var totalKeys = prefill + keys_needed_for_inserts + 1000; // 1000 needed for some rounding error?

List<Measurement> measurements = new();
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

    Console.Write($"start {numThreads} threads ... ");
    var keysPerThread = keys_needed_for_inserts / config.Threads;
    var measurement = Bench.RunWorkload(mix, config, keys, keysPerThread);
    measurements.Add(measurement);
    Console.WriteLine($"avg: {measurement.AvLatency} ns");
}

var csvFilePath = "latency99.csharp.csv";

using (var writer = new StreamWriter(csvFilePath))
{
    writer.WriteLine("name,total_ops,threads,latency");

    foreach (var m in measurements)
    {
        var row = string.Format($"c#,{m.ThreadCount},{m.AvLatency}");
        writer.WriteLine(row);

        var code_line = string.Format("Measurement {{ name: \"c#\", thread_count: {0}, latency: {1} }},",
            m.ThreadCount,
            m.AvLatency
        );

        Console.WriteLine(code_line);
    }

    
}

Console.WriteLine("CSV file written successfully!");