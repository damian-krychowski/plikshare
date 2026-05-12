using System.Collections.Concurrent;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using PlikShare.Core.Utils;
using PlikShare.EmailProviders.ExternalProviders.Resend;

#pragma warning disable CS8604 // Possible null reference argument.

namespace PlikShare.IntegrationTests.Infrastructure;

public class ResendEmailServer : IAsyncDisposable
{
    private bool _disposed;
    public int PortNumber { get; }
    public WebApplication App { get; }
    public string AppUrl { get; }
    
    // Thread-safe collection to store received emails
    public ConcurrentBag<ReceivedRequest> ReceivedEmails { get; } = new();

    // Recipients whose next /emails request must fail with HTTP 500. Tests use this to
    // exercise the synchronous-send rollback path on the FE workspace invite.
    private readonly ConcurrentDictionary<string, byte> _addressesToFail = new(StringComparer.OrdinalIgnoreCase);

    public ResendEmailServer(int portNumber)
    {
        PortNumber = portNumber;

        var builder = WebApplication.CreateBuilder();
        AppUrl = $"https://localhost:{PortNumber}";
        builder.WebHost.UseUrls(AppUrl);

        App = builder.Build();

        App.MapPost("/emails", async (HttpContext context) =>
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();
            var resendRequestBody = Json.Deserialize<ResendRequestBody>(
                body);

            var shouldFail = resendRequestBody.To
                .Any(to => _addressesToFail.ContainsKey(to));

            if (shouldFail)
            {
                // Do not record failed sends — the production sender treats non-2xx as a
                // throw, so from PlikShare's perspective the email never went out.
                return Results.StatusCode(StatusCodes.Status500InternalServerError);
            }

            ReceivedEmails.Add(new ReceivedRequest(
                Body: resendRequestBody,
                AuthorizationHeader: context.Request.Headers.Authorization));

            return TypedResults.Ok();
        });

        App.Start();
    }

    /// <summary>
    /// Causes the next (and any subsequent) /emails request targeting the given recipient
    /// to fail with HTTP 500. Use <see cref="ClearFailures"/> at the end of the test to
    /// avoid bleeding into other tests on the shared fixture.
    /// </summary>
    public void FailEmailsTo(string emailAddress) => _addressesToFail[emailAddress] = 0;

    public void ClearFailures() => _addressesToFail.Clear();

    public void ClearReceivedEmails() => ReceivedEmails.Clear();

    public IEnumerable<CapturedEmail> AllCapturedEmails() =>
        ReceivedEmails.Select(r => new CapturedEmail(
            To: r.Body.To,
            Subject: r.Body.Subject,
            Html: r.Body.Html));

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        
        try
        {
            await App.StopAsync();
            await App.DisposeAsync();
        }
        finally
        {
            _disposed = true;
        }
    }

    public record ReceivedRequest(
        ResendRequestBody Body,
        string? AuthorizationHeader);

    public void ShouldContainEmails(List<ResendRequestBody> emails)
    {
        foreach (var email in emails)
        {
            ReceivedEmails
                .Should()
                .ContainEquivalentOf(new ReceivedRequest(
                        Body: email,
                        AuthorizationHeader: null),
                    opt => opt.Excluding(x => x.AuthorizationHeader));
        }
    }

    public ReceivedRequest? GetLastEmailTo(string userEmail)
    {
        return ReceivedEmails.LastOrDefault(email =>
            email.Body.To.Contains(userEmail, StringComparer.OrdinalIgnoreCase));
    }
}