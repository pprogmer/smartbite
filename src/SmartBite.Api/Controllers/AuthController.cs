using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartBite.Contracts;
using SmartBite.Data;

namespace SmartBite.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(SmartBiteDbContext db, IConfiguration config) : ControllerBase
{
    private readonly PasswordHasher<User> _hasher = new();

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(AuthRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            return Problem("Користувач із такою адресою вже існує", statusCode: 409);
        }

        var user = new User { Email = email };
        user.PasswordHash = _hasher.HashPassword(user, request.Password);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return new AuthResponse(CreateToken(user), user.Email);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(AuthRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email);
        if (
            user is null
            || _hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password)
                == PasswordVerificationResult.Failed
        )
        {
            return Problem("Неправильна адреса або пароль", statusCode: 401);
        }

        return new AuthResponse(CreateToken(user), user.Email);
    }

    private string CreateToken(User user) =>
        new JsonWebTokenHandler().CreateToken(
            new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity([new Claim("sub", user.Id.ToString())]),
                Expires = DateTime.UtcNow.AddHours(2),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
                    SecurityAlgorithms.HmacSha256
                ),
            }
        );
}
