using System;

namespace Spittoon;

/// <summary>
/// Represents an exception that occurs during Spittoon operations.
/// </summary>
public class SpittoonException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpittoonException"/> class.
    /// </summary>
    public SpittoonException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpittoonException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SpittoonException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpittoonException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public SpittoonException(string message, Exception innerException) : base(message, innerException) { }
}