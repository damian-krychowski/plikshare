using Microsoft.AspNetCore.Mvc;

namespace PlikShare.Core.Authorization;

public class RequireAppOwnerAttribute() : TypeFilterAttribute(typeof(RequireAppOwnerFilter));