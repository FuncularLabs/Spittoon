using System;

namespace Spittoon
{
    /// <summary>
    /// Represents a syntax error exception that occurs during Spittoon parsing.
    /// </summary>
    public class SpittoonSyntaxException : SpittoonException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpittoonSyntaxException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SpittoonSyntaxException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpittoonSyntaxException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public SpittoonSyntaxException(string message, Exception innerException) : base(message, innerException) { }
    }
}
