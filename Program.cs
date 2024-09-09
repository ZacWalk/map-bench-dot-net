using map_bench_dot_net;

var spec = Mix.ReadHeavy();
var mix = spec.ToOps();
var capacity = 1_000_000;
var totalOps = capacity * 55;
var prefill = capacity / 2;
var totalKeys = prefill + (totalOps * spec.Insert / 100) + 100;

List<Measurement> measurements = new();
var keys = new Bench.Keys(totalKeys);

for (var i = 0; i < Environment.ProcessorCount; i++)
{
    var config = new Bench.RunConfig
    {
        Threads = i + 1,
        InitialCapacity = capacity,
        TotalOps = totalOps,
        Prefill = prefill
    };

    measurements.Add(Bench.RunWorkload(mix, config, keys, true));
}

var csvFilePath = "latency95.csharp.csv";

using (var writer = new StreamWriter(csvFilePath))
{
    writer.WriteLine("name,total_ops,threads,latency");

    foreach (var m in measurements)
    {
        var row = string.Format("{0},{1},{2},{3}",
            "c#",
            m.TotalOps,
            m.ThreadCount,
            m.Latency
        );

        writer.WriteLine(row);

        var code_line = string.Format("Measurement {{ name: \"c#\", total_ops: {0}, thread_count: {1}, latency: {2} }},",
            m.TotalOps,
            m.ThreadCount,
            m.Latency
        );

        Console.WriteLine(code_line);
    }

    
}

Console.WriteLine("CSV file written successfully!");