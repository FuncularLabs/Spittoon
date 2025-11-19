using System;
using System.Collections;
using System.Collections.Generic;

namespace Spittoon.Nodes
{
    public sealed class SpittoonArrayNode : SpittoonNode, IEnumerable<SpittoonNode>
    {
        private readonly List<SpittoonNode> _items = new();
        public IReadOnlyList<SpittoonNode> Items => _items;

        public SpittoonArrayNode() { NodeType = SpittoonNodeType.Array; }

        public void Add(SpittoonNode node)
        {
            if (node.Parent != null && !ReferenceEquals(node.Parent, this)) throw new InvalidOperationException("Node already has a parent");
            node.Parent = this;
            node.Index = _items.Count;
            node.PropertyName = null;
            _items.Add(node);
        }

        public IEnumerator<SpittoonNode> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => $"[{_items.Count} items]";
    }
}
