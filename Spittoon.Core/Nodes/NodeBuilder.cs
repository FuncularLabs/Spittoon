using System.Collections.Generic;

namespace Spittoon.Nodes
{
    internal static class NodeBuilder
    {
        public static SpittoonNode Build(object? raw)
        {
            switch (raw)
            {
                case null:
                    return new SpittoonValueNode(null);
                case Dictionary<string, object?> dict:
                    var objNode = new SpittoonObjectNode();
                    foreach (var kv in dict)
                        objNode.Add(kv.Key, Build(kv.Value));
                    return objNode;
                case List<object?> list:
                    var arrayNode = new SpittoonArrayNode();
                    foreach (var item in list)
                        arrayNode.Add(Build(item));
                    return arrayNode;
                default:
                    return new SpittoonValueNode(raw);
            }
        }
    }
}
