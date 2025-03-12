using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace PlikShare.Core.Authorization;

public class RequireClaimAttribute : TypeFilterAttribute
{
    public RequireClaimAttribute(string claimType, string claimValue) : base(typeof(RequireClaimFilter))
    {
        Arguments = [new Claim(claimType, claimValue)];
    }
}