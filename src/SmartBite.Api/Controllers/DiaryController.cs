using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Data;

namespace SmartBite.Api.Controllers;

/// <summary>Changes to entries return the updated day, so the summary is refreshed at once (US-5, US-9).</summary>
[ApiController]
[Route("api/diary")]
public class DiaryController(SmartBiteDbContext db, DiaryService diary) : ControllerBase
{
    /// <summary>Entries and summary for a date (today by default).</summary>
    [HttpGet]
    public Task<DiaryDayResponse> GetDay(DateOnly? date) =>
        diary.GetDayAsync(User.GetId(), date ?? DiaryService.Today);

    /// <summary>Calories for the 7 days ending with <paramref name="end"/> (today by default).</summary>
    [HttpGet("week")]
    public Task<WeekStatsResponse> GetWeek(DateOnly? end) =>
        diary.GetWeekAsync(User.GetId(), end ?? DiaryService.Today);

    [HttpPost]
    public async Task<DiaryDayResponse> Add(DiaryEntryRequest request)
    {
        var entry = new DiaryEntry { UserId = User.GetId() };
        Apply(entry, request);
        db.DiaryEntries.Add(entry);
        await db.SaveChangesAsync();

        return await diary.GetDayAsync(entry.UserId, entry.Date);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<DiaryDayResponse>> Update(int id, DiaryEntryRequest request)
    {
        var entry = await FindAsync(id);
        if (entry is null)
        {
            return NotFoundProblem();
        }

        Apply(entry, request);
        await db.SaveChangesAsync();
        return await diary.GetDayAsync(entry.UserId, entry.Date);
    }

    [HttpDelete("{id:int}")]
    public async Task<ActionResult<DiaryDayResponse>> Delete(int id)
    {
        var entry = await FindAsync(id);
        if (entry is null)
        {
            return NotFoundProblem();
        }

        db.DiaryEntries.Remove(entry);
        await db.SaveChangesAsync();
        return await diary.GetDayAsync(entry.UserId, entry.Date);
    }

    private Task<DiaryEntry?> FindAsync(int id)
    {
        var userId = User.GetId();
        return db.DiaryEntries.SingleOrDefaultAsync(e => e.Id == id && e.UserId == userId);
    }

    private ObjectResult NotFoundProblem() => Problem("Запис не знайдено", statusCode: 404);

    private static void Apply(DiaryEntry entry, DiaryEntryRequest request)
    {
        entry.Date = request.Date;
        entry.MealType = request.MealType;
        entry.Name = request.Name.Trim();
        entry.Source = request.Source;
        entry.Servings = request.Servings;
        entry.Calories = request.PerServing.Calories;
        entry.Protein = request.PerServing.Protein;
        entry.Fat = request.PerServing.Fat;
        entry.Carbs = request.PerServing.Carbs;
    }
}
