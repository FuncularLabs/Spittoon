using System;

namespace Spittoon.Nodes
{
    public sealed class SpittoonValueNode : SpittoonNode
    {
        public object? Value { get; }

        public SpittoonValueNode(object? value)
        {
            NodeType = SpittoonNodeType.Value;
            Value = value;
        }

        public override string ToString() => Value?.ToString() ?? "null";

        public string AsString() => Value?.ToString() ?? string.Empty;
        public long AsInt64() => Value is long l ? l : Convert.ToInt64(Value);
    }
}
