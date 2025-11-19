using System;
using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Spittoon;

namespace Spittoon.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args) => BenchmarkRunner.Run<SpittoonBenchmarks>();
    }

    [MemoryDiagnoser]
    [InProcess]
    public class SpittoonBenchmarks
    {
        private readonly string smallDoc = "server:{ host:localhost, port:8080, secure:false };";
        private readonly string tabularDoc = "users:{ header:{ id:int, name:str }; }: [ [1, Alice], [2, Bob], [3, Carol] ]";
        private readonly string largeArray;

        public SpittoonBenchmarks()
        {
            var sb = new StringBuilder();
            sb.Append("arr:[");
            // Increased to 100,000 elements to make parsing dominant in CPU profile
            for (int i = 0; i < 100_000; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(i);
            }
            sb.Append("]");
            largeArray = sb.ToString();
        }

        [Benchmark]
        public object? ParseSmall()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Forgiving);
            return d.Parse(smallDoc);
        }

        [Benchmark]
        public object? ParseTabular()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Forgiving);
            return d.Parse(tabularDoc);
        }

        [Benchmark]
        public object? ParseLargeArray()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Strict);
            return d.Parse(largeArray);
        }
    }
}
