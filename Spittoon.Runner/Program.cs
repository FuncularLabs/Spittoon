using System;
using System.Text;
using System.Diagnostics;
using Spittoon;

namespace Spittoon.Runner
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Build a very large array string to stress the parser
            var sb = new StringBuilder();
            sb.Append("arr:[");
            for (int i = 0; i < 500_000; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(i);
            }
            sb.Append(']');
            string large = sb.ToString();

            Console.WriteLine("Starting parse workload...");

            var sw = Stopwatch.StartNew();
            var d = new SpittoonDeserializer(SpittoonMode.Strict);

            // Run several iterations to make the workload obvious in the profiler
            for (int iter = 0; iter < 5; iter++)
            {
                var result = d.Parse(large);
                Console.WriteLine($"Iteration {iter} parsed. Type: {result?.GetType().Name}");
            }

            sw.Stop();
            Console.WriteLine($"Total time: {sw.Elapsed}");
        }
    }
}
