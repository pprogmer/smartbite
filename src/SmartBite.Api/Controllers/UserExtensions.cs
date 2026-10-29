using System.Security.Claims;

namespace SmartBite.Api.Controllers;

public static class UserExtensions
{
    /// <summary>Id of the signed-in user from the "sub" claim of the token.</summary>
    public static int GetId(this ClaimsPrincipal user) => int.Parse(user.FindFirstValue("sub")!);
}
