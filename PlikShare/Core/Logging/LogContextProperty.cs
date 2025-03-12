namespace PlikShare.Core.Logging;

public static class LogContextProperty
{
    public const string OriginalRequestPath = "OriginalRequestPath";
    public const string RemoteIpAddress = "RemoteIpAddress";
    public const string IsHttps = "IsHttps";
    public const string HttpProtocol = "HttpProtocol";
    
    public const string CorrelationId = "CorrelationId";
    public const string JobIdentity = "JobIdentity";
    public const string QueueConsumerId = "QueueConsumerId";
    
    public const string AuthPolicy = "AuthPolicy";
    public const string UserId = "UserId";
    public const string UserExternalId = "UserExternalId";
    public const string UserEmail = "UserEmail";

    public const string WorkspaceId = "WorkspaceId";
    public const string WorkspaceExternalId = "WorkspaceExternalId";
    public const string IsWorkspaceOwnedByUser = "IsWorkspaceOwnedByUser";
    public const string WorkspacePermissions = "WorkspacePermissions";

    public const string BoxId = "BoxId";
    public const string BoxExternalId = "BoxExternalId";
    public const string BoxLinkExternalId = "BoxLinkExternalId";
    
    public const string BoxAccess = "BoxAccess";
    public const string AccessCode = "AccessCode";

    public const string BoxLinkSessionId = "BoxLinkSessionId";

    public const string PreSignedUrlId = "PreSignedUrlId";
    public const string PreSignedUrlOwner = "PreSignedUrlOwner";
}