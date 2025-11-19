using System.IO;
using System.Reflection;

namespace Spittoon.Tests;

public static class Embedded
{
    public static string Read(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var s = asm.GetManifestResourceStream(resourceName) ?? throw new FileNotFoundException(resourceName);
        using var r = new StreamReader(s);
        return r.ReadToEnd();
    }
}
