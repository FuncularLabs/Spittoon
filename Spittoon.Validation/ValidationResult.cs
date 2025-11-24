// Spittoon.Validation — the righteous judge of data, now with perfect syntax

using System;
using System.Collections.Generic;
using System.Linq;

namespace Spittoon.Validation
{
    /// <summary>
    /// Represents the result of a validation operation.
    /// </summary>
    public sealed class ValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the validation was successful.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the list of validation errors.
        /// </summary>
        public IReadOnlyList<ValidationError> Errors { get; }

        private ValidationResult(bool isValid, List<ValidationError> errors)
        {
            IsValid = isValid;
            Errors = errors.AsReadOnly();
        }

        /// <summary>
        /// Creates a successful validation result.
        /// </summary>
        /// <returns>A successful validation result.</returns>
        public static ValidationResult Success() => new(true, []);

        /// <summary>
        /// Creates a failed validation result with the specified errors.
        /// </summary>
        /// <param name="errors">The list of errors.</param>
        /// <returns>A failed validation result.</returns>
        public static ValidationResult Fail(List<ValidationError> errors) => new(false, errors);
    }
}