using System;
namespace Spittoon.Attributes
{
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Class)]
    public sealed class SpittoonIgnoreAttribute : Attribute { }
}
