using Spittoon;

namespace Spittoon.Nodes
{
    public sealed class SpittoonDocument
    {
        public SpittoonNode Root { get; }

        internal SpittoonDocument(SpittoonNode root) => Root = root;

        public static SpittoonDocument Load(string text) => Load(text, SpittoonMode.Forgiving);

        public static SpittoonDocument Load(string text, SpittoonMode mode)
        {
            var raw = new SpittoonDeserializer(mode).Parse(text);
            var rootNode = NodeBuilder.Build(raw);
            return new SpittoonDocument(rootNode);
        }
    }
}
