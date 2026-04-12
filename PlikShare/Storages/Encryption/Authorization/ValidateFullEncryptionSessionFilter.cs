using Microsoft.AspNetCore.DataProtection;
using PlikShare.Core.Encryption;
using PlikShare.Core.Utils;
using PlikShare.Workspaces.Validation;

namespace PlikShare.Storages.Encryption.Authorization;

public class ValidateFullEncryptionSessionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next)
    {
        var workspace = context.HttpContext.GetWorkspaceMembershipDetails().Workspace;

        if (workspace.Storage.EncryptionType != StorageEncryptionType.Full)
            return await next(context);

        var cookieName = FullEncryptionSessionCookie.GetCookieName(
            workspace.Storage.ExternalId);

        if (!context.HttpContext.Request.Cookies.TryGetValue(cookieName, out var cookieValue)
            || string.IsNullOrEmpty(cookieValue))
        {
            return HttpErrors.Storage.FullEncryptionSessionRequired();
        }

        var protector = context.HttpContext.RequestServices
            .GetRequiredService<IDataProtectionProvider>()
            .CreateProtector(FullEncryptionSessionCookie.Purpose);

        byte[] kek;
        try
        {
            var protectedBytes = Convert.FromBase64String(cookieValue);
            kek = protector.Unprotect(protectedBytes);
        }
        catch
        {
            return HttpErrors.Storage.FullEncryptionSessionRequired();
        }

        var session = new FullEncryptionSession { Kek = kek };
        context.HttpContext.Items[FullEncryptionSession.HttpContextName] = session;

        return await next(context);
    }
}
