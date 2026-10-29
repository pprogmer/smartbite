using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using SmartBite.Api.Services;
using SmartBite.Contracts;

namespace SmartBite.Api.Controllers;

[ApiController]
[Route("api/nutrition")]
public class NutritionController(EstimateService estimates) : ControllerBase
{
    private const string Unavailable = "Не вдалося оцінити страву. Спробуйте пізніше";

    [HttpPost("estimate/text")]
    public async Task<ActionResult<EstimateResponse>> EstimateText(
        EstimateTextRequest request,
        CancellationToken ct
    )
    {
        try
        {
            var estimate = await estimates.EstimateTextAsync(request.Text, ct);
            return estimate is null
                ? Problem("У тексті не знайдено продуктів", statusCode: 422)
                : estimate;
        }
        catch (OpenRouterException)
        {
            return Problem(Unavailable, statusCode: 503);
        }
    }

    /// <summary>Takes a JPEG or PNG up to 5 MB as the "photo" form field. The photo is not stored.</summary>
    [HttpPost("estimate/photo")]
    public async Task<ActionResult<EstimateResponse>> EstimatePhoto(
        [Required(ErrorMessage = "Виберіть фотографію")] IFormFile photo,
        CancellationToken ct
    )
    {
        byte[]? bytes = null;
        if (photo.Length <= EstimateLimits.MaxPhotoBytes)
        {
            using var buffer = new MemoryStream();
            await photo.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }

        var mediaType = bytes is null ? null : EstimateService.DetectImageType(bytes);
        if (mediaType is null)
        {
            ModelState.AddModelError(
                nameof(photo),
                "Фотографія має бути у форматі JPEG або PNG розміром до 5 МБ"
            );
            return ValidationProblem();
        }

        try
        {
            var estimate = await estimates.EstimatePhotoAsync(bytes!, mediaType, ct);
            return estimate is null
                ? Problem("На фотографії не виявлено страви", statusCode: 422)
                : estimate;
        }
        catch (OpenRouterException)
        {
            return Problem(Unavailable, statusCode: 503);
        }
    }

    /// <summary>Recalculates an estimate after the user changed masses or removed components.</summary>
    [HttpPost("recalculate")]
    public EstimateResponse Recalculate(RecalculateRequest request) =>
        EstimateService.Recalculate(request);
}
