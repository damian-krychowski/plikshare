using PlikShare.Files.Metadata;

namespace PlikShare.Tests;

public class FileMetadataJsonScannerTests
{
    [Fact]
    public void GetThumbnailMiniEtag_returns_etag_for_mini_thumbnail()
    {
        var json = """{"$type":"thumbnail","etag":"8luHuowl6MaAz8J0J4-CBA","variant":"Mini"}""";

        Assert.Equal(
            "8luHuowl6MaAz8J0J4-CBA",
            FileMetadataJsonScanner.GetThumbnailMiniEtag(json));
    }

    [Fact]
    public void GetThumbnailMiniEtag_is_property_order_independent()
    {
        var json = """{"variant":"Mini","etag":"abc","$type":"thumbnail"}""";

        Assert.Equal(
            "abc",
            FileMetadataJsonScanner.GetThumbnailMiniEtag(json));
    }

    [Theory]
    [InlineData("mini")]
    [InlineData("Mini")]
    public void GetThumbnailMiniEtag_accepts_both_variant_string_casings(string variant)
    {
        var json = $$"""{"$type":"thumbnail","etag":"abc","variant":"{{variant}}"}""";

        Assert.Equal(
            "abc",
            FileMetadataJsonScanner.GetThumbnailMiniEtag(json));
    }

    [Fact]
    public void GetThumbnailMiniEtag_accepts_numeric_mini_variant()
    {
        var json = """{"$type":"thumbnail","etag":"abc","variant":0}""";

        Assert.Equal(
            "abc",
            FileMetadataJsonScanner.GetThumbnailMiniEtag(json));
    }

    [Theory]
    [InlineData("Small")]
    [InlineData("Large")]
    public void GetThumbnailMiniEtag_returns_null_for_other_variants(string variant)
    {
        var json = $$"""{"$type":"thumbnail","etag":"abc","variant":"{{variant}}"}""";

        Assert.Null(FileMetadataJsonScanner.GetThumbnailMiniEtag(json));
    }

    [Theory]
    [InlineData("""{"$type":"image-dimensions","width":800,"height":600}""")]
    [InlineData("""{"$type":"aws-textract-result","jobs":["a","b"]}""")]
    public void GetThumbnailMiniEtag_returns_null_for_other_metadata_types(string json)
    {
        Assert.Null(FileMetadataJsonScanner.GetThumbnailMiniEtag(json));
    }

    [Fact]
    public void GetThumbnailMiniEtag_returns_null_when_etag_is_missing()
    {
        var json = """{"$type":"thumbnail","variant":"Mini"}""";

        Assert.Null(FileMetadataJsonScanner.GetThumbnailMiniEtag(json));
    }

    [Fact]
    public void GetThumbnailMiniEtag_skips_unknown_properties_including_nested_objects()
    {
        var json = """{"$type":"thumbnail","extra":{"nested":{"variant":"Small"}},"list":[1,2],"etag":"abc","variant":"Mini"}""";

        Assert.Equal(
            "abc",
            FileMetadataJsonScanner.GetThumbnailMiniEtag(json));
    }

    [Fact]
    public void GetImageDimensions_returns_width_and_height()
    {
        var json = """{"$type":"image-dimensions","width":800,"height":600}""";

        Assert.Equal(
            new FileMetadataJsonScanner.ImageDimensions(Width: 800, Height: 600),
            FileMetadataJsonScanner.GetImageDimensions(json));
    }

    [Fact]
    public void GetImageDimensions_is_property_order_independent()
    {
        var json = """{"height":600,"width":800,"$type":"image-dimensions"}""";

        Assert.Equal(
            new FileMetadataJsonScanner.ImageDimensions(Width: 800, Height: 600),
            FileMetadataJsonScanner.GetImageDimensions(json));
    }

    [Theory]
    [InlineData("""{"$type":"thumbnail","etag":"abc","variant":"Mini"}""")]
    [InlineData("""{"$type":"aws-textract-result","jobs":[]}""")]
    public void GetImageDimensions_returns_null_for_other_metadata_types(string json)
    {
        Assert.Null(FileMetadataJsonScanner.GetImageDimensions(json));
    }

    [Theory]
    [InlineData("""{"$type":"image-dimensions","width":800}""")]
    [InlineData("""{"$type":"image-dimensions","height":600}""")]
    public void GetImageDimensions_returns_null_when_a_dimension_is_missing(string json)
    {
        Assert.Null(FileMetadataJsonScanner.GetImageDimensions(json));
    }

    [Fact]
    public void GetImageDimensions_skips_unknown_properties()
    {
        var json = """{"$type":"image-dimensions","extra":{"width":1},"width":800,"height":600}""";

        Assert.Equal(
            new FileMetadataJsonScanner.ImageDimensions(Width: 800, Height: 600),
            FileMetadataJsonScanner.GetImageDimensions(json));
    }
}
