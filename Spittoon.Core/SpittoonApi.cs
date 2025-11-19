using System;
using System.Collections.Generic;

namespace Spittoon
{
    /// <summary>
    /// Minimal public API facade for Spittoon library to satisfy consumers.
    /// This file provides a small, stable surface while fuller implementations
    /// are repaired in other source files.
    /// </summary>
    public enum SpittoonMode
    {
        Strict,
        Forgiving
    }
}
