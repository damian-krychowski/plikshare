namespace PlikShare.Boxes.UpdateDefaultDisplayConfiguration.Contracts;

public record UpdateBoxDefaultDisplayConfigurationRequestDto(
    BoxViewMode ViewMode,
    BoxSortMode SortMode,
    BoxSortDirection SortDirection,
    bool ThumbnailsEnabled);
