using JiraLite.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace JiraLite.Api.Services;

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _http = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var user = _http.HttpContext?.User;
            if (user == null) return null;

            var idClaim =
                user.FindFirstValue("id") ??
                user.FindFirstValue(ClaimTypes.NameIdentifier);

            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
    }
}
