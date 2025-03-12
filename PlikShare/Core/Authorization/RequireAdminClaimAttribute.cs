using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace PlikShare.Core.Authorization;

public class RequireAdminClaimAttribute : TypeFilterAttribute
{
    public RequireAdminClaimAttribute(string claimType, string claimValue) : base(typeof(RequireAdminClaimFilter))
    {
        Arguments = [new Claim(claimType, claimValue)];
    }
}