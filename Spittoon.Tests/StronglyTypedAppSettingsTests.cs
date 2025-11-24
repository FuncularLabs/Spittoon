using System.Reflection;
using Xunit;

namespace Spittoon.Tests;

public class StronglyTypedAppSettingsTests
{
    [Fact]
    public void SerializeAndDeserialize_AppSettings_RoundTrip()
    {
        var text = Embedded.Read("Spittoon.Tests.Fixtures.appsettings.spit");
        var des = new Spittoon.SpittoonDeserializer(Spittoon.SpittoonMode.Forgiving);
        var typed = des.Deserialize<AppSettingsDocument>(text);

        var ser = new Spittoon.SpittoonSerializer(Spittoon.SpittoonMode.Forgiving);
        var outText = ser.Serialize(typed, Spittoon.Formatting.Indented);

        Assert.Contains("Users", outText);
        Assert.Contains("AppSettings", outText);
    }

    [Fact]
    public void Validate_AppSettings_AgainstSchema()
    {
        var schemaText = Embedded.Read("Spittoon.Tests.Fixtures.appsettings.spitsd");
        var validator = new Spittoon.Validation.SpitsdValidator(schemaText);
        var data = Embedded.Read("Spittoon.Tests.Fixtures.appsettings.spit");
        var result = validator.Validate(data, Spittoon.SpittoonMode.Strict);
        Assert.True(result.IsValid);
    }
}
