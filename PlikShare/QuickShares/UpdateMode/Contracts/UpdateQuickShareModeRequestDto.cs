namespace PlikShare.QuickShares.UpdateMode.Contracts;

public record UpdateQuickShareModeRequestDto(
    QuickShareMode Mode,
    bool AllowIndividualFileDownload);
