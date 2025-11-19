namespace Spittoon.Validation;

/// <summary>
/// Represents a validation error with a path and message.
/// </summary>
public sealed class ValidationError
{
    /// <summary>
    /// Gets the path where the error occurred.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the error message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationError"/> class.
    /// </summary>
    /// <param name="path">The path to the error.</param>
    /// <param name="message">The error message.</param>
    public ValidationError(string path, string message)
    {
        Path = path;
        Message = message;
    }

    /// <summary>
    /// Returns a string representation of the error.
    /// </summary>
    /// <returns>The string representation.</returns>
    public override string ToString() => $"{Path}: {Message}";
}