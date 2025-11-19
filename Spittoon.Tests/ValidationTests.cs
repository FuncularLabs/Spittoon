using Spittoon.Validation;
using Xunit;

namespace Spittoon.Tests;

public class ValidationTests
{
    private const string ContestSchema = @"
schema:{
  type: obj;
  properties:{
    entries:{
      type: arr{1,100};
      uniqueItems: true;
      items:{
        type: obj;
        properties:{
          contestant:{ type: str; minLength:5 };
          distance:{ type: float; min:0; max:50.0; exclusiveMax:true };
          style:{ type: str; enum:[arc, straight, spin] };
          witnesses:{ type: arr{2,5}; items:{ type: str } };
        };
        required:[contestant, distance];
        additionalProperties: false;
      };
    };
  };
}";

    private readonly SschValidator _validator = new(ContestSchema);

    [Fact]
    public void Valid_ContestEntry_Passes()
    {
        const string data = @"
entries:[
  {
    contestant:Gus McCracken;
    distance:9.8;
    style:spin;
    witnesses:[Witness 1, Witness 2, Witness 3]
  }
]";

        var result = _validator.Validate(data);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Invalid_ContestEntry_Fails_With_Multiple_Errors()
    {
        const string data = @"
entries:[
  {
    contestant:Joe;           // too short
    distance:75.0;            // exceeds max
    style:helicopter;          // not in enum
    witnesses:[OnlyOne];
    polish:shiny              // additional property
  }
]";

        var result = _validator.Validate(data);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Message.Contains("too short"));
        Assert.Contains(result.Errors, e => e.Message.Contains("maximum"));
        Assert.Contains(result.Errors, e => e.Message.Contains("enum"));
        Assert.Contains(result.Errors, e => e.Message.Contains("least 2"));
        Assert.Contains(result.Errors, e => e.Message.Contains("additional"));
    }
}