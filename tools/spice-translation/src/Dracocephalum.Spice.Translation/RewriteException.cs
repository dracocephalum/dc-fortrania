namespace Dracocephalum.Spice.Translation;

/// <summary>
/// Thrown where a translated file is not as a rewrite expects it, which
/// means the toolkit's sources or the translator changed in a way the
/// rewrite has to be told about.
/// </summary>
public sealed class RewriteException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RewriteException"/> class.</summary>
    public RewriteException()
        : this("A translated file is not as the rewrite expects it.")
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RewriteException"/> class.</summary>
    /// <param name="message">What is not as expected.</param>
    public RewriteException(string message)
        : base(message)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="RewriteException"/> class.</summary>
    /// <param name="message">What is not as expected.</param>
    /// <param name="innerException">The exception that led to this one.</param>
    public RewriteException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
