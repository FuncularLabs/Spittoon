using System;
using System.Collections.Generic;

namespace Spittoon.Nodes
{
    public enum SpittoonNodeType { Object, Array, Value }
    
    public abstract class SpittoonNode
    {
        public SpittoonNodeType NodeType { get; protected set; }
        public SpittoonNode? Parent { get; internal set; }
        internal string? PropertyName { get; set; }
        internal int Index { get; set; } = -1;

        public string Path
        {
            get
            {
                var parts = new Stack<string>();
                SpittoonNode? current = this;
                while (current != null)
                {
                    string part = current.Parent == null ? "<root>" : current.PropertyName ?? (current.Index >= 0 ? $"[{current.Index}]" : current.ToString() ?? "<value>");
                    parts.Push(part);
                    current = current.Parent;
                }
                return string.Join("/", parts);
            }
        }

        public bool IsObject() => NodeType == SpittoonNodeType.Object;
        public bool IsArray() => NodeType == SpittoonNodeType.Array;

        public SpittoonObjectNode AsObject() => this as SpittoonObjectNode ?? throw new InvalidCastException();
        public SpittoonArrayNode AsArray() => this as SpittoonArrayNode ?? throw new InvalidCastException();
        public SpittoonValueNode AsValue() => this as SpittoonValueNode ?? throw new InvalidCastException();

        public abstract override string ToString();
    }
}
