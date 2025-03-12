using PlikShare.Boxes.Cache;
using PlikShare.Boxes.Permissions;
using PlikShare.BoxLinks.Id;

namespace PlikShare.BoxLinks.Cache;

public record  BoxLinkContext(
    int Id,
    BoxLinkExtId ExternalId,
    string Name,
    bool IsEnabled,
    BoxPermissions Permissions,
    BoxContext Box);