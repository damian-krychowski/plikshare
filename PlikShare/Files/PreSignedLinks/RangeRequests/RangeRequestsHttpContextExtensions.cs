using Serilog;

namespace PlikShare.Files.PreSignedLinks.RangeRequests;

public static class RangeRequestsHttpContextExtensions
{
    public static RangeRequest TryGetRangeRequest(
        this HttpContext httpContext, 
        long fileSizeInBytes)
    {
        var rangeHeader = httpContext.Request.Headers.Range.ToString();

        if (string.IsNullOrEmpty(rangeHeader) || !rangeHeader.StartsWith("bytes="))
        {
            return new RangeRequest(IsRangeRequest: false);
        }

        var ranges = rangeHeader["bytes=".Length..].Split(',');

        if (ranges.Length != 1) // We'll only handle single ranges for now
        {
            Log.Warning("Not supported: Range request with more than one range was requested: {@Ranges}", ranges);

            return new RangeRequest(IsRangeRequest: false);
        }

        var range = ranges[0].Trim();
        var parts = range.Split('-');

        if (parts.Length != 2)
        {
            return new RangeRequest(IsRangeRequest: false);
        }

        long start, end;

        if (string.IsNullOrEmpty(parts[0]))
        {
            // Suffix range: -500 means last 500 bytes
            start = fileSizeInBytes - long.Parse(parts[1]);
            end = fileSizeInBytes - 1;
        }
        else
        {
            start = long.Parse(parts[0]);
            end = string.IsNullOrEmpty(parts[1]) ? fileSizeInBytes - 1 : long.Parse(parts[1]);
        }

        return new RangeRequest(
            IsRangeRequest: true,
            Range: new BytesRange(
                Start: start,
                End: end));
    }
}

public readonly record struct RangeRequest(
    bool IsRangeRequest,
    BytesRange Range = default)
{

    public bool IsValid(long fileSizeInBytes)
    {
        return Range.Start >= 0
               && Range.End < fileSizeInBytes
               && Range.Start <= Range.End;
    }

    public string ValidContentRange(long fileSizeInBytes) => $"bytes {Range.Start}-{Range.End}/{fileSizeInBytes}";
    public string InvalidContentRange(long fileSizeInBytes) => $"bytes */{fileSizeInBytes}";
}