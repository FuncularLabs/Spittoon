using System.Collections.Generic;
using Xunit;
using Spittoon;

namespace Spittoon.Tests.Parser
{
    // Tests that require forgiving parsing behavior
    public class ForgivingParserTests
    {
        [Fact]
        public void UnterminatedQuotedString_Forgiving_ReturnsContent()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Forgiving);
            var v = d.Parse("\"{");
            Assert.Equal("{", v);
        }

        [Fact]
        public void UnbracedRoot_Forgiving_ParsesPairs()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Forgiving);
            var obj = d.Parse("a:1; b:2") as Dictionary<string, object?>;
            Assert.NotNull(obj);
            Assert.Equal(2, obj.Count);
            Assert.Equal(1L, obj["a"]);
            Assert.Equal(2L, obj["b"]);
        }

        [Fact]
        public void UnquotedValueWithSpaces_Forgiving_ReturnsFullString()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Forgiving);
            var v = d.Parse("Bob Smith");
            Assert.Equal("Bob Smith", v);
        }

        [Fact]
        public void BraceCardinalityToken_IsParsedAsSingleUnquotedToken()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Forgiving);
            var v = d.Parse("arr{1,100}");
            Assert.Equal("arr{1,100}", v);
        }
    }
}
