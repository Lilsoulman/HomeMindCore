namespace HomeMind.Business.IServices.Connector;

/// <summary>
/// Indicates that a note-detail request was accepted by the local XHS bridge but
/// cannot be fulfilled because the copied link is invalid or the note is unavailable.
/// </summary>
public sealed class XhsNoteDetailException : Exception
{
    /// <summary>Initializes the exception with its public HTTP status and safe message.</summary>
    public XhsNoteDetailException(int statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    /// <summary>HTTP status code suitable for the API response.</summary>
    public int StatusCode { get; }
}
