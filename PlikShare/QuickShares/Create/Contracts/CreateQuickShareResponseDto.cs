using PlikShare.QuickShares.Id;

namespace PlikShare.QuickShares.Create.Contracts;

public record CreateQuickShareResponseDto(
    QuickShareExtId ExternalId,
    string Slug,
    string Url);
