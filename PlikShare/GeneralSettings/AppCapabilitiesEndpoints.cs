using PlikShare.Core.Authorization;
using PlikShare.Files.Thumbnails.Generation;

namespace PlikShare.GeneralSettings;

/// <summary>
/// Frontend-facing read of build- and runtime-detected capabilities: features that depend on
/// optional dependencies (currently ffmpeg, future: HEIC/video, etc.). Distinct from
/// <c>application-settings</c> (admin-only, deployment state) and <c>entry-page</c> (anonymous,
/// pre-login). One round-trip after sign-in and frontend caches the result for the session.
/// </summary>
public static class AppCapabilitiesEndpoints
{
    public static void MapAppCapabilitiesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/app-capabilities")
            .WithTags("App Capabilities")
            .RequireAuthorization(policyNames: AuthPolicy.Internal);

        group.MapGet("/", GetCapabilities)
            .WithName("GetAppCapabilities");
    }

    private static GetAppCapabilitiesResponseDto GetCapabilities(
        FfmpegService ffmpegService)
    {
        return new GetAppCapabilitiesResponseDto
        {
            IsFfmpegAvailable = ffmpegService.IsAvailable
        };
    }

    public class GetAppCapabilitiesResponseDto
    {
        public required bool IsFfmpegAvailable { get; init; }
    }
}
