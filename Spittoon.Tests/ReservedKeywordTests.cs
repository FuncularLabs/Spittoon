using Xunit;
using System.Collections.Generic;

namespace Spittoon.Tests;

public class ReservedKeywordTests
{
    [Fact]
    public void ModelWithHeaderAndRowsProperties_Roundtrip()
    {
        var text = @"{ doc: { header: { title: str }, rows: [ { title: 'A' }, { title: 'B' } ] } }";
        var des = new Spittoon.SpittoonDeserializer(Spittoon.SpittoonMode.Forgiving);
        var dyn = des.DeserializeDynamic(text);
        // Map to a strongly-typed holder
        var obj = new Wrapper();
        var typed = des.Deserialize<Wrapper>(text);
        Assert.NotNull(typed);
    }

    [Fact]
    public void QuotedHeaderKey_TreatedAsNormalProperty()
    {
        var text = "{ \"header\": { title: str }, doc: { header: { title: str } } }";
        var des = new Spittoon.SpittoonDeserializer(Spittoon.SpittoonMode.Forgiving);
        var parsed = des.Parse(text);
        Assert.IsType<Dictionary<string, object?>>(parsed);
        var dict = (Dictionary<string, object?>)parsed!;
        Assert.True(dict.ContainsKey("header"));
        Assert.True(dict.ContainsKey("doc"));
    }
}

public class Wrapper
{
    public TabularModel doc { get; set; } = new();
}

public class TabularModel
{
    public Dictionary<string, string> header { get; set; } = new();
    public List<Dictionary<string, string>> rows { get; set; } = new();
}
