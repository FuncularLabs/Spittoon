namespace Spittoon.Validation;

/// <summary>
/// Provides static methods for validating Spittoon syntax.
/// </summary>
public static class SpittoonValidator
{
    /// <summary>
    /// Determines whether the specified text is valid Spittoon syntax.
    /// </summary>
    /// <param name="text">The text to validate.</param>
    /// <param name="mode">The validation mode.</param>
    /// <returns>True if valid; otherwise, false.</returns>
    public static bool IsValid(string text, SpittoonMode mode = SpittoonMode.Strict)
    {
        try
        {
            new SpittoonDeserializer(mode).Parse(text);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Validates the syntax of the specified text.
    /// </summary>
    /// <param name="text">The text to validate.</param>
    /// <returns>The validation result.</returns>
    public static ValidationResult ValidateSyntax(string text)
    {
        try
        {
            new SpittoonDeserializer().Parse(text);
            return ValidationResult.Success();
        }
        catch (SpittoonSyntaxException ex)
        {
            return ValidationResult.Fail(new List<ValidationError> { new ValidationError("<root>", ex.Message) });
        }
    }
}