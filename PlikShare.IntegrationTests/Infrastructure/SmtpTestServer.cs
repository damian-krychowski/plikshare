using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MimeKit;

namespace PlikShare.IntegrationTests.Infrastructure;

public class SmtpTestServer : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private bool _disposed;

    public int PortNumber { get; }
    public string Hostname => "localhost";

    public ConcurrentBag<ReceivedSmtpEmail> ReceivedEmails { get; } = new();

    public SmtpTestServer(int portNumber)
    {
        PortNumber = portNumber;
        _listener = new TcpListener(IPAddress.Loopback, portNumber);
        _listener.Start();
        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    public ReceivedSmtpEmail? GetLastEmailTo(string recipient)
    {
        return ReceivedEmails.LastOrDefault(email =>
            email.RcptTo.Any(r => r.Equals(recipient, StringComparison.OrdinalIgnoreCase)));
    }

    public void ClearReceivedEmails() => ReceivedEmails.Clear();

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            await _cts.CancelAsync();
            _listener.Stop();
            try { await _acceptLoop; } catch { /* expected on shutdown */ }
        }
        finally
        {
            _disposed = true;
            _cts.Dispose();
        }
    }

    private async Task AcceptLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = await _listener.AcceptTcpClientAsync(ct);
                _ = Task.Run(() => HandleClientAsync(client, ct), ct);
            }
            catch (OperationCanceledException) { return; }
            catch (ObjectDisposedException) { return; }
            catch (SocketException) { return; }
        }
    }

    // BOM-less UTF-8: StreamWriter would otherwise emit the byte-order mark on the very
    // first write, which makes MailKit fail with "Unable to parse status code" because it
    // expects a clean "220 ..." banner.
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private async Task HandleClientAsync(TcpClient tcpClient, CancellationToken ct)
    {
        using var _ = tcpClient;
        await using var stream = tcpClient.GetStream();
        using var reader = new StreamReader(stream, Utf8NoBom);
        await using var writer = new StreamWriter(stream, Utf8NoBom)
        {
            NewLine = "\r\n",
            AutoFlush = true
        };

        await writer.WriteLineAsync("220 localhost ESMTP TestServer ready");

        string? authUsername = null;
        string? authPassword = null;
        string? mailFrom = null;
        var rcptTo = new List<string>();

        while (!ct.IsCancellationRequested)
        {
            string? line;
            try
            {
                line = await reader.ReadLineAsync(ct);
            }
            catch (OperationCanceledException) { return; }
            catch (IOException) { return; }

            if (line is null) return;

            var upper = line.ToUpperInvariant();

            if (upper.StartsWith("EHLO") || upper.StartsWith("HELO"))
            {
                await writer.WriteLineAsync("250-localhost");
                await writer.WriteLineAsync("250-AUTH LOGIN PLAIN");
                await writer.WriteLineAsync("250 OK");
            }
            else if (upper.StartsWith("AUTH LOGIN"))
            {
                // Optional inline initial response: "AUTH LOGIN <base64-username>"
                var parts = line.Split(' ', 3);
                if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    authUsername = TryDecodeBase64(parts[2]);
                }
                else
                {
                    await writer.WriteLineAsync("334 VXNlcm5hbWU6"); // "Username:"
                    var encodedUser = await reader.ReadLineAsync(ct);
                    authUsername = TryDecodeBase64(encodedUser ?? string.Empty);
                }

                await writer.WriteLineAsync("334 UGFzc3dvcmQ6"); // "Password:"
                var encodedPassword = await reader.ReadLineAsync(ct);
                authPassword = TryDecodeBase64(encodedPassword ?? string.Empty);

                await writer.WriteLineAsync("235 Authentication successful");
            }
            else if (upper.StartsWith("AUTH PLAIN"))
            {
                var parts = line.Split(' ', 3);
                string base64Payload;

                if (parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[2]))
                {
                    base64Payload = parts[2];
                }
                else
                {
                    await writer.WriteLineAsync("334 ");
                    base64Payload = await reader.ReadLineAsync(ct) ?? string.Empty;
                }

                DecodeAuthPlain(base64Payload, out authUsername, out authPassword);
                await writer.WriteLineAsync("235 Authentication successful");
            }
            else if (upper.StartsWith("MAIL FROM:"))
            {
                mailFrom = ExtractAddress(line);
                await writer.WriteLineAsync("250 OK");
            }
            else if (upper.StartsWith("RCPT TO:"))
            {
                rcptTo.Add(ExtractAddress(line));
                await writer.WriteLineAsync("250 OK");
            }
            else if (upper == "DATA")
            {
                await writer.WriteLineAsync("354 Start mail input; end with <CRLF>.<CRLF>");

                var raw = new StringBuilder();
                while (true)
                {
                    string? dataLine;
                    try { dataLine = await reader.ReadLineAsync(ct); }
                    catch (OperationCanceledException) { return; }
                    catch (IOException) { return; }

                    if (dataLine is null) return;
                    if (dataLine == ".") break;

                    // Reverse dot-stuffing per RFC 5321 §4.5.2
                    if (dataLine.StartsWith("..")) dataLine = dataLine[1..];

                    raw.Append(dataLine).Append("\r\n");
                }

                ReceivedEmails.Add(BuildReceivedEmail(
                    authUsername,
                    authPassword,
                    mailFrom ?? string.Empty,
                    rcptTo.ToArray(),
                    raw.ToString()));

                mailFrom = null;
                rcptTo.Clear();

                await writer.WriteLineAsync("250 Message accepted");
            }
            else if (upper.StartsWith("RSET"))
            {
                mailFrom = null;
                rcptTo.Clear();
                await writer.WriteLineAsync("250 OK");
            }
            else if (upper.StartsWith("NOOP"))
            {
                await writer.WriteLineAsync("250 OK");
            }
            else if (upper.StartsWith("QUIT"))
            {
                await writer.WriteLineAsync("221 Bye");
                return;
            }
            else
            {
                await writer.WriteLineAsync("502 Command not implemented");
            }
        }
    }

    private static ReceivedSmtpEmail BuildReceivedEmail(
        string? authUsername,
        string? authPassword,
        string mailFrom,
        string[] rcptTo,
        string rawMime)
    {
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(rawMime));
        var message = MimeMessage.Load(ms);

        return new ReceivedSmtpEmail(
            AuthenticatedUsername: authUsername,
            AuthenticatedPassword: authPassword,
            MailFrom: mailFrom,
            RcptTo: rcptTo,
            Subject: message.Subject ?? string.Empty,
            HtmlBody: message.HtmlBody ?? message.TextBody ?? string.Empty);
    }

    private static string TryDecodeBase64(string value)
    {
        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static void DecodeAuthPlain(string base64, out string username, out string password)
    {
        username = string.Empty;
        password = string.Empty;

        byte[] raw;
        try { raw = Convert.FromBase64String(base64); }
        catch (FormatException) { return; }

        // Format per RFC 4616: <authzid>\0<authcid>\0<password>
        var firstNull = Array.IndexOf(raw, (byte)0);
        if (firstNull < 0) return;

        var secondNull = Array.IndexOf(raw, (byte)0, firstNull + 1);
        if (secondNull < 0) return;

        username = Encoding.UTF8.GetString(raw, firstNull + 1, secondNull - firstNull - 1);
        password = Encoding.UTF8.GetString(raw, secondNull + 1, raw.Length - secondNull - 1);
    }

    private static string ExtractAddress(string line)
    {
        var lt = line.IndexOf('<');
        var gt = line.IndexOf('>', lt + 1);
        return (lt >= 0 && gt > lt) ? line[(lt + 1)..gt] : line.Trim();
    }

    public record ReceivedSmtpEmail(
        string? AuthenticatedUsername,
        string? AuthenticatedPassword,
        string MailFrom,
        string[] RcptTo,
        string Subject,
        string HtmlBody);
}
