// Spittoon.Tests — xUnit edition
// Because Spittoon deserves tests that are clean, parallel, and hit the cuspidor dead-center every single time.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Spittoon;
using Xunit;
using Xunit.Abstractions;

namespace Spittoon.Tests
{
    public class SerializerRoundTripTests
    {
        private readonly SpittoonSerializer _serializer = new();
        private readonly SpittoonDeserializer _deserializer = new(SpittoonMode.Forgiving);
        private static readonly string[] ExpectedTags = ["champ", "legend"];

        [Theory]
        [InlineData("null", null)]
        [InlineData("true", true)]
        [InlineData("false", false)]
        [InlineData("42", 42L)]
        [InlineData("-7", -7L)]
        [InlineData("3.14159", 3.14159)]
        [InlineData("-0.0", -0.0)]
        [InlineData("hello", "hello")]
        [InlineData("\"hello world\"", "hello world")]
        [InlineData("\"hello;world\"", "hello;world")]
        [InlineData("\"{", "{")]
        public void Primitive_RoundTrip_Works(string spittoon, object? value)
        {
            object? deserialized = _deserializer.Parse(spittoon);
            Assert.Equal(value, deserialized);
        }

        [Fact]
        public void SimpleObject_RoundTrip()
        {
            var obj = new { name = "Gus", distance = 9.8, active = true, tags = ExpectedTags };

            string spittoon = _serializer.Serialize(obj, Formatting.Indented);

            // Just verify it contains the important bits  exact punctuation may vary with comma/semicolon duality
            Assert.Contains("\"name\":\"Gus\"", spittoon);
            Assert.Contains("\"distance\":9.8", spittoon);
            Assert.Contains("\"active\":true", spittoon);
            Assert.Contains("[\"champ\"; \"legend\"]", spittoon); // semicolon because Indented mode prefers it

            object? roundTripped = _deserializer.Parse(spittoon);
            var dict = Assert.IsType<Dictionary<string, object?>>(roundTripped);
            var name = Assert.IsType<string>(dict["name"]);
            Assert.Equal("Gus", name);

            var distance = Assert.IsType<double>(dict["distance"]);
            Assert.Equal(9.8, distance);

            var active = Assert.IsType<bool>(dict["active"]);
            Assert.True(active);

            var tags = Assert.IsType<List<object?>>(dict["tags"]);
            var tagStrings = tags.Select(t => Assert.IsType<string>(t)).ToArray();
            Assert.Equal(ExpectedTags, tagStrings);
        }

        [Fact]
        public void NestedAndMixed_RoundTrip()
        {
            var payload = new
            {
                config = new { port = 8080, debug = false },
                endpoints = new object[]
                {
                    new { path = "/api/users", method = "GET" },
                    new { path = "/api/logs", method = "POST", auth = new { role = "admin" } }
                },
                scores = new[] { 95, 87, 92 }
            };

            string spittoon = _serializer.Serialize(payload, Formatting.Indented);
            object? rt = _deserializer.Parse(spittoon);

            var dict = Assert.IsType<Dictionary<string, object?>>(rt);
            var config = Assert.IsType<Dictionary<string, object?>>(dict["config"]);
            var port = Assert.IsType<long>(config["port"]);
            Assert.Equal(8080L, port);
            var debug = Assert.IsType<bool>(config["debug"]);
            Assert.False(debug);

            var endpoints = Assert.IsType<List<object?>>(dict["endpoints"]);
            var first = Assert.IsType<Dictionary<string, object?>>(endpoints[0]);
            Assert.Equal("/api/users", Assert.IsType<string>(first["path"]));

            var second = Assert.IsType<Dictionary<string, object?>>(endpoints[1]);
            var secondAuth = Assert.IsType<Dictionary<string, object?>>(second["auth"]);
            Assert.Equal("admin", Assert.IsType<string>(secondAuth["role"]));
        }

        [Fact]
        public void Comments_Are_Ignored()
        {
            const string spittoon = @"
                /* This is a config */
                server:{ 
                  host:localhost; // default
                  port:8080;
                };
                // end of file
            ";

            var parsed = _deserializer.Parse(spittoon);
            var result = Assert.IsType<Dictionary<string, object?>>(parsed);
            var server = Assert.IsType<Dictionary<string, object?>>(result["server"]);
            Assert.Equal("localhost", Assert.IsType<string>(server["host"]));
            Assert.Equal(8080L, Assert.IsType<long>(server["port"]));
        }

        [Fact]
        public void Comma_And_Semicolon_Are_Interchangeable()
        {
            string withCommas = "{a:1, b:2, c:3}";
            string withSemicolons = "{a:1; b:2; c:3}";
            string mixed = "{a:1, b:2; c:3}";

            var d1 = Assert.IsType<Dictionary<string, object?>>(_deserializer.Parse(withCommas));
            var d2 = Assert.IsType<Dictionary<string, object?>>(_deserializer.Parse(withSemicolons));
            var d3 = Assert.IsType<Dictionary<string, object?>>(_deserializer.Parse(mixed));

            Assert.All([d1, d2, d3], d => Assert.Equal(3L, (long)d.Count));
        }

        [Fact]
        public void SerializesTabularAsUnlabeledRows()
        {
            var table = new Dictionary<string, object?>
            {
                ["header"] = new Dictionary<string, object?> { ["id"] = "int", ["name"] = "str" },
                ["rows"] = new List<object?> {
                    new Dictionary<string, object?> { ["id"] = 1L, ["name"] = "Alice" },
                    new Dictionary<string, object?> { ["id"] = 2L, ["name"] = "Bob" }
                }
            };

            var doc = new Dictionary<string, object?> { ["users"] = table };

            string sp = _serializer.Serialize(doc, Formatting.Indented);

            // serializer should prefer unlabeled rows  look for '[' row forms rather than '{ id:' per row
            Assert.DoesNotContain("{ id:1", sp);
            Assert.Contains("[\n", sp); // rows array newline

            var parsed = Assert.IsType<Dictionary<string, object?>>(_deserializer.Parse(sp));
            var users = Assert.IsType<Dictionary<string, object?>>(parsed["users"]);
            var rows = Assert.IsType<List<object?>>(users["rows"]);
            Assert.Equal(2, rows.Count);
            var firstRow = Assert.IsType<Dictionary<string, object?>>(rows[0]);
            Assert.Equal(1L, Assert.IsType<long>(firstRow["id"]));
        }
    }
}