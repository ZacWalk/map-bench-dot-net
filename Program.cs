using map_bench_dot_net;
using System.Runtime;

Dictionary<string, List<Measurement>> measurements = new Dictionary<string, List<Measurement>>();

measurements["PERF_DATA_DOT_NET_99_10k"] = RunTest(Mix.Read99(), 10000);
measurements["PERF_DATA_DOT_NET_99_1M"] = RunTest(Mix.Read99(), 1000000);
measurements["PERF_DATA_DOT_NET_100_10K"] = RunTest(Mix.ReadOnly(), 10000);
measurements["PERF_DATA_DOT_NET_100_1M"] = RunTest(Mix.ReadOnly(), 1000000);

WriteResults(measurements);

List<Measurement> RunTest(Mix mix, int num_start_items)
{
    var ops = mix.ToOps();
    var totalOps = 40000000;
    var prefill = num_start_items;
    var expected_inserts = (totalOps * mix.Insert / 100);
    var capacity = num_start_items + expected_inserts;
    var totalKeys = prefill + expected_inserts + 1000; // 1000 needed for some rounding error?

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

        Console.Write($" {mix.Read}% {num_start_items,7} {numThreads,2} threads ... ");
        var keysPerThread = expected_inserts / config.Threads;
        var measurement = Bench.RunWorkload(ops, config, keys, keysPerThread);
        measurements.Add(measurement);
        Console.WriteLine($"avg: {measurement.AvLatency} ns");
    }

    return measurements;
}

void WriteResults(Dictionary<string, List<Measurement>> measurements)
{
    var rustFile = "perf_dotnet_data.rs";

    using (var writer = new StreamWriter(rustFile))
    {
        writer.WriteLine("use std::sync::LazyLock;");
        writer.WriteLine("use crate::perf::Measurement;");
        writer.WriteLine("");

        foreach (var mm in measurements)
        {
            writer.WriteLine($"pub static {mm.Key}: LazyLock<Vec<Measurement>> = LazyLock::new(|| {{  vec![");

            foreach (var m in mm.Value)
            {
                var code_line = string.Format("Measurement {{ name: \"c#\", thread_count: {0}, latency: {1} }},",
                    m.ThreadCount,
                    m.AvLatency
                );

                writer.WriteLine(code_line);
            }

            writer.WriteLine("] });");
        }
    }

}