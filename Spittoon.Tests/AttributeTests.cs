using Spittoon.Attributes;
using Xunit;

namespace Spittoon.Tests;

public class AttributeTests
{
    private class TestClass
    {
        public string Keep { get; set; } = "";

        [SpittoonIgnore]
        public string IgnoreMe { get; set; } = "secret";

        [SpittoonRequired]
        public string MustHave { get; set; } = "";

        [SpittoonName("custom-name")]
        public string Renamed { get; set; } = "";
    }

    private readonly SpittoonSerializer _serializer = new();

    [Fact]
    public void Attributes_Respected_In_Serialization()
    {
        var obj = new TestClass
        {
            Keep = "visible",
            IgnoreMe = "hidden",
            MustHave = "present",
            Renamed = "value"
        };

        string spittoon = _serializer.Serialize(obj);

        Assert.Contains("\"Keep\":\"visible\"", spittoon);
        Assert.DoesNotContain("\"IgnoreMe\"", spittoon);
        Assert.Contains("\"MustHave\":\"present\"", spittoon);
        Assert.Contains("\"custom-name\":\"value\"", spittoon);
    }
}