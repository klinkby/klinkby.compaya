namespace Klinkby.Compaya;

/// <summary>
///     Failure to send SMS
/// </summary>
public class CompayaSmsException : Exception
{
    /// <summary>
    ///     Failure to send SMS
    /// </summary>
    public CompayaSmsException()
    {
    }

    /// <summary>
    ///     Failure to send SMS
    /// </summary>
    public CompayaSmsException(string message) : base(message)
    {
    }

    /// <summary>
    ///     Failure to send SMS
    /// </summary>
    public CompayaSmsException(string message, Exception innerException) : base(message, innerException)
    {
    }
}