using Spittoon.Nodes;
using Xunit;

namespace Spittoon.Tests;

public class NodeGraphTests
{
    [Fact]
    public void Document_Load_And_Path_Work()
    {
        const string spittoon = "{user:{name:Alice; stats:{score:100}}}";

        var doc = SpittoonDocument.Load(spittoon);

        var nameNode = doc.Root.AsObject()["user"].AsObject()["name"].AsValue();
        Assert.Equal("<root>/user/name", nameNode.Path);
        Assert.Equal("Alice", nameNode.AsString());

        var scoreNode = doc.Root.AsObject()["user"].AsObject()["stats"].AsObject()["score"].AsValue();
        Assert.Equal("<root>/user/stats/score", scoreNode.Path);
        Assert.Equal(100L, scoreNode.AsInt64());
    }
}