using System;
namespace Spittoon.Attributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class SpittoonRequiredAttribute : Attribute
    {
        public string? ErrorMessage { get; set; }
    }
}
