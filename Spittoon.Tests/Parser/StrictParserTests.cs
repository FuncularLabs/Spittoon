using System.Collections.Generic;
using Xunit;
using Spittoon;

namespace Spittoon.Tests.Parser
{
    // Tests for strict parsing behavior
    public class StrictParserTests
    {
        [Fact]
        public void UnterminatedQuotedString_Strict_Throws()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Strict);
            Assert.Throws<Spittoon.SpittoonSyntaxException>(() => d.Parse("\"{"));
        }

        [Fact]
        public void UnbracedRoot_Strict_Throws_If_NotAllowed()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Strict);
            // In strict mode unbraced root parsing is not allowed; parse should throw
            Assert.Throws<Spittoon.SpittoonSyntaxException>(() => d.Parse("a:1; b:2"));
        }

        [Fact]
        public void UnquotedValueWithSpaces_Strict_ThrowsOnTrailing()
        {
            var d = new SpittoonDeserializer(SpittoonMode.Strict);
            Assert.Throws<Spittoon.SpittoonSyntaxException>(() => d.Parse("Bob Smith"));
        }
    }
}
