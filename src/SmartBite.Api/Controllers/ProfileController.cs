using Microsoft.AspNetCore.Mvc;
using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Data;

namespace SmartBite.Api.Controllers;

[ApiController]
[Route("api/profile")]
public class ProfileController(SmartBiteDbContext db) : ControllerBase
{
    /// <summary>404 means the profile is not filled in yet.</summary>
    [HttpGet]
    public async Task<ActionResult<ProfileResponse>> Get()
    {
        var profile = await db.Profiles.FindAsync(User.GetId());
        return profile is null
            ? Problem("Профіль ще не заповнено", statusCode: 404)
            : ToResponse(profile);
    }

    [HttpPut]
    public async Task<ProfileResponse> Save(ProfileRequest request)
    {
        var userId = User.GetId();
        var profile = await db.Profiles.FindAsync(userId);
        if (profile is null)
        {
            profile = new Profile { UserId = userId };
            db.Profiles.Add(profile);
        }

        profile.Sex = request.Sex;
        profile.Age = request.Age;
        profile.HeightCm = request.HeightCm;
        profile.WeightKg = request.WeightKg;
        profile.ActivityLevel = request.ActivityLevel;
        profile.Goal = request.Goal;
        await db.SaveChangesAsync();

        return ToResponse(profile);
    }

    private static ProfileResponse ToResponse(Profile p) =>
        new(
            p.Sex,
            p.Age,
            p.HeightCm,
            p.WeightKg,
            p.ActivityLevel,
            p.Goal,
            NormCalculator.Calculate(p)
        );
}
