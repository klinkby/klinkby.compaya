using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Web;

namespace Klinkby.Compaya;

/// <summary>
///     Send SMS using Compaya SMS service
/// </summary>
public class CompayaSms
{
    private const string ServiceUrl = "https://www.cpsms.dk/sms/";
    private static readonly CompayaSms Empty = new();

    private static readonly Regex Regex = new(
        @"(\<error\>(?'error'[^\<]+)\</error\>)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture);

    private CompayaSms()
    {
    }

    /// <summary>
    ///     Specifies the parameters for sending the sms
    /// </summary>
    /// <param name="recipient">
    ///     Specifies the recipient of the SMS. Multiple Recipients can be provided by submitting multiple
    ///     recipient fields named "recipient[]". Each recipient number has to be 8 chars, all numeric. When sending the SMS,
    ///     the server will prepend 0045 to all numbers.
    /// </param>
    /// <param name="message">
    ///     Max len 459. Specifies the message to be sent. All chars allowed by the SMS protocol are
    ///     accepted. If the message contains any illegal chars, they are automatically removed, and the message shortened.
    ///     Multiple spaces are trimmed to only a single space. The maximum message length is 459 characters, which is the
    ///     length of 3 sms'es joined together.
    /// </param>
    /// <param name="username">Login name to your SMS account</param>
    /// <param name="password">Password to your SMS account</param>
    public CompayaSms(
        string username,
        string password,
        long recipient,
        string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentNullException(nameof(message));
        }
        Username = username;
        Password = password;
        Recipient = recipient;
        if (message.Length > 459)
        {
            throw new ArgumentOutOfRangeException(nameof(message));
        }

        Message = message;
    }

    // Required
    /// <summary>Specify the username to your SMS account</summary>
    /// <remarks>Required</remarks>
    public string? Username { get; private set; }

    /// <summary>Specify the password to your SMS account</summary>
    /// <remarks>Required</remarks>
    public string? Password { get; private set; }

    /// <summary>
    ///     Specifies the recipient of the SMS. Multiple Recipients can be provided by submitting multiple recipient
    ///     fields named "recipient[]". Each recipient number has to be 8 chars, all numeric. When sending the SMS, the server
    ///     will prepend 0045 to all numbers.
    /// </summary>
    /// <remarks>Required</remarks>
    public long Recipient { get; private set; }

    /// <summary>
    ///     Max len 459. Specifies the message to be sent. All chars allowed by the SMS protocol are accepted. If the
    ///     message contains any illegal chars, they are automatically removed, and the message shortened. Multiple spaces are
    ///     trimmed to only a single space. The maximum message length is 459 characters, which is the length of 3 sms'es
    ///     joined together.
    /// </summary>
    public string? Message { get; private set; }

    // Optional
    /// <summary>
    ///     Set the number that the receiver will see as the sender of the SMS. It can be both numeric and alphanumeric,
    ///     and has a limit of 11 chars.
    /// </summary>
    public string? From { get; set; }

    /// <summary>Specifies whether the SMS is a flash SMS. A value of 1 sets the SMS to be a flash SMS.</summary>
    public byte Flash { get; set; }

    /// <summary>
    ///     This is NOT a forwarding url. The url value specifies the url to open for delivery notification. The url will be
    ///     opened with a status parameter as well as a cell phone number appended in a HTTP GET request. So if the url to be
    ///     opened is http://www.google.com/ the delivery will be http://www.google.com/?status=X&amp;receiver=XXXXXXXX The status
    ///     parameter can have the following values:
    ///     <list>
    ///         <item>1 Delivery successful</item><item>2 Delivery failure</item>4 Message buffered<item></item>
    ///     </list>
    ///     In other words 1 means succesfull, 4 means in process, and 2 means the sms failed. Max 100 chars.
    /// </summary>
    public Uri? Url { get; set; }

    /// <summary>
    ///     For sending delayed SMS, you can supply a timestamp to define when the SMS should be sent.
    ///     The timestamp format is: YYYYMMDDHHMM So for instance the value 200805051215 would be sent on the 5th of May 2008
    ///     at 12:15.
    /// </summary>
    public long Timestamp { get; set; }

    /// <summary>
    ///     Specifies whether the message and from parameters are UTF8 encoded. A value of 1 sets the SMS to be UTF8
    ///     encoded.
    /// </summary>
    public byte Utf8 { get; set; }

    /// <inheritdoc />
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase", Justification = "Lower case is required")]
    public override string ToString()
    {
        PropertyInfo[] ps = typeof(CompayaSms).GetProperties(BindingFlags.Instance | BindingFlags.Public);
        string[] psArr = ps.Where(p => !Equals(p.GetValue(this, null), p.GetValue(Empty, null)))
            .Select(
                p =>
                    p.Name.ToLowerInvariant() + "=" +
                    HttpUtility.UrlEncode(p.GetValue(this, null).ToString()))
            .ToArray();
        return string.Join("&", psArr);
    }

    /// <summary>Sends the sms non-blocking using I/O completion port</summary>
    /// <param name="cancellationToken">Stop the background process</param>
    /// <exception cref="CompayaSmsException">Thrown if SMS couldn't be sent</exception>
    public async Task SendAsync(CancellationToken cancellationToken)
    {
        Uri reqUri = new(ServiceUrl + "?" + ToString());
        string contents = await GetHttpContentsAsync(reqUri, cancellationToken).ConfigureAwait(false);
        Group errorGroup = Regex.Match(contents).Groups["error"];
        if (errorGroup.Success)
        {
            throw new CompayaSmsException(errorGroup.Value);
        }
    }
    
    private static async Task<string> GetHttpContentsAsync(Uri reqUri, CancellationToken cancellationToken)
    {
        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(reqUri);
        req.Method = "GET";
        using HttpWebResponse res = (HttpWebResponse)await GetResponseAsync(req, cancellationToken).ConfigureAwait(false);
        if (res.StatusCode >= HttpStatusCode.BadRequest)
        {
            throw new CompayaSmsException($"{res.StatusDescription} ({res.StatusCode})");
        }

        Stream? s = res.GetResponseStream();
        if (s == null || 0 == res.ContentLength)
        {
            return "";
        }

        using StreamReader sr = new(s);
        string contents = await sr.ReadToEndAsync().ConfigureAwait(false);

        return contents;
    }

    private static async Task<WebResponse> GetResponseAsync(WebRequest request, CancellationToken ct)
    {
        using (ct.Register(request.Abort, false))
        {
            try
            {
                WebResponse response = await request.GetResponseAsync().ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                return (HttpWebResponse)response;
            }
            catch (WebException ex)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException(ex.Message, ex, ct);
                }

                throw;
            }
        }
    }
}