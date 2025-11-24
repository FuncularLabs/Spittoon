using System;
using Xunit;

namespace Spittoon.Tests;

public class StronglyTypedDeserializationTests
{
    private readonly SpittoonDeserializer _deserializer = new(SpittoonMode.Forgiving);
    private static readonly string[] ExpectedTags = ["dev", "music"];

    private class Person
    {
        public string Name { get; set; } = "";
        public int Age { get; set; }
        public bool Active { get; set; }
        public string[] Tags { get; set; } = [];
    }

    [Fact]
    public void Poco_Deserializes_Correctly()
    {
        const string spittoon = "name:Alice; age:30; active:true; tags:[dev, music]";

        var person = _deserializer.Deserialize<Person>(spittoon);

        Assert.Equal("Alice", person.Name);
        Assert.Equal(30, person.Age);
        Assert.True(person.Active);
        Assert.Equal(ExpectedTags, person.Tags);
    }
}