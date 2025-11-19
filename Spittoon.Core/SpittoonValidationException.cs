namespace Spittoon;

/// <summary>
/// Represents a validation error exception that occurs during Spittoon deserialization.
/// </summary>
public class SpittoonValidationException : SpittoonException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpittoonValidationException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SpittoonValidationException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpittoonValidationException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public SpittoonValidationException(string message, Exception innerException) : base(message, innerException) { }
}