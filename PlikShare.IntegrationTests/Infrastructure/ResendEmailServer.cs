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

            ReceivedEmails.Add(new ReceivedRequest(
                Body: resendRequestBody,
                AuthorizationHeader: context.Request.Headers.Authorization));

            return TypedResults.Ok();
        });

        App.Start();
    }

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
            ReceivedEmails.Should().ContainEquivalentOf(new ReceivedRequest(
                Body: email,
                AuthorizationHeader: default), opt => opt.Excluding(x => x.AuthorizationHeader));
        }
    }
}