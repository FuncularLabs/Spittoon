using System;
using System.Collections;
using System.Collections.Generic;

namespace Spittoon.Nodes
{
    public sealed class SpittoonObjectNode : SpittoonNode, IEnumerable<KeyValuePair<string, SpittoonNode>>
    {
        private readonly Dictionary<string, SpittoonNode> _properties = new(StringComparer.Ordinal);
        public IReadOnlyDictionary<string, SpittoonNode> Properties => _properties;

        public SpittoonObjectNode() { NodeType = SpittoonNodeType.Object; }

        public SpittoonNode this[string key]
        {
            get => _properties[key];
            set
            {
                if (value.Parent != null && value.Parent != this) throw new InvalidOperationException("Node already has a parent");
                value.Parent = this;
                value.PropertyName = key;
                value.Index = -1;
                _properties[key] = value;
            }
        }

        public void Add(string key, SpittoonNode node)
        {
            if (_properties.ContainsKey(key)) throw new ArgumentException($"Duplicate key '{key}'");
            this[key] = node;
        }

        public IEnumerator<KeyValuePair<string, SpittoonNode>> GetEnumerator() => _properties.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"{{{string.Join(",", _properties.Keys)}}}";
    }
}
