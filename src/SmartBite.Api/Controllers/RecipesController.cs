using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Data;

namespace SmartBite.Api.Controllers;

[ApiController]
[Route("api/recipes")]
public class RecipesController(SmartBiteDbContext db, DiaryService diary, RecipeService recipes)
    : ControllerBase
{
    /// <summary>A recipe from the selected products that fits the rest of the norm for the date (today by default).</summary>
    [HttpPost("generate")]
    public async Task<ActionResult<RecipeResponse>> Generate(
        RecipeRequest request,
        CancellationToken ct
    )
    {
        var userId = User.GetId();
        var ids = request.ProductIds.Distinct().ToList();
        var products = await db
            .Products.Where(p => p.UserId == userId && ids.Contains(p.Id))
            .ToListAsync(ct);
        if (products.Count != ids.Count)
        {
            return Problem("Деякі з вибраних продуктів не знайдено", statusCode: 404);
        }

        var day = await diary.GetDayAsync(userId, request.Date ?? DiaryService.Today);
        if (day.Remaining is null)
        {
            return Problem("Спочатку заповніть профіль", statusCode: 409);
        }
        if (day.Remaining.Calories <= 0)
        {
            return Problem("Добову норму на цей день уже вичерпано", statusCode: 409);
        }

        try
        {
            return await recipes.GenerateAsync(products, request.Servings, day.Remaining, ct);
        }
        catch (OpenRouterException)
        {
            return Problem("Не вдалося згенерувати рецепт. Спробуйте пізніше", statusCode: 503);
        }
    }
}
