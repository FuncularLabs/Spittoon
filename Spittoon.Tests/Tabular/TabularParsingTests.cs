using Xunit;
using Spittoon;
using Spittoon.Nodes;

namespace Spittoon.Tests.Tabular
{
    public class TabularParsingTests
    {
        [Fact]
        public void ParsesTabularHeaderAndRows()
        {
            const string text = @"
                users:{ header:{ id:int, name:str }; }: [
                  { id:1, name:Alice },
                  { id:2, name:Bob }
                ]
            ";

            var doc = SpittoonDocument.Load(text, SpittoonMode.Forgiving);
            var root = doc.Root.AsObject();
            var users = root["users"].AsObject();
            var rows = users["rows"].AsArray();

            Assert.Equal(2, rows.Items.Count);
            foreach (var r in rows.Items) Assert.True(r.IsObject() || r.IsArray());
        }

        [Fact]
        public void ParsesUnlabeledRows()
        {
            const string text = @"
                users:{ header:{ id:int, name:str }; }: [
                  [1, Alice],
                  [2, Bob]
                ]
            ";

            var doc = SpittoonDocument.Load(text, SpittoonMode.Forgiving);
            var root = doc.Root.AsObject();
            var users = root["users"].AsObject();
            var rows = users["rows"].AsArray();

            Assert.Equal(2, rows.Items.Count);
            foreach (var r in rows.Items) Assert.True(r.IsObject() || r.IsArray());
        }

        [Fact]
        public void InvalidRowFailsValidation()
        {
            const string schema = @"
schema:{
  schema:{
    users:{ type: obj; properties:{ header:{ type: obj }; rows:{ type: arr }; } }
  }
};
";
            var validator = new Spittoon.Validation.SschValidator(schema);

            const string data = @"
users:{ header:{ id:int, name:str }; }: [
  { id:1, name:Alice },
  { id:2 } // missing name
]
";
            var result = validator.Validate(data);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }

        [Fact]
        public void InvalidUnlabeledRowFailsValidation()
        {
            const string schema = @"
schema:{
  schema:{
    users:{ type: obj; properties:{ header:{ type: obj }; rows:{ type: arr }; } }
  }
};
";
            var validator = new Spittoon.Validation.SschValidator(schema);

            const string data = @"
users:{ header:{ id:int, name:str }; }: [
  [1, Alice],
  [2] // missing name
]
";
            var result = validator.Validate(data);
            Assert.False(result.IsValid);
            Assert.NotEmpty(result.Errors);
        }
    }
}
