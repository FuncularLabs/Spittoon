using System;
namespace Spittoon.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SpittoonNameAttribute : Attribute
    {
        public SpittoonNameAttribute(string name) => Name = name;
        public string Name { get; }
    }
}
