using Microsoft.Extensions.Options;
using SmartBite.Contracts;

namespace SmartBite.Api.Services;

/// <summary>An estimate as the model returns it.</summary>
public record EstimateDraft(bool IsFood, string DishName, List<ComponentDraft> Components);

public record ComponentDraft(string Name, decimal Grams, Nutrients Per100g);

public class EstimateService(OpenRouterClient openRouter, IOptions<OpenRouterOptions> options)
{
    private const string Instructions = """
        Ти дієтолог. Визнач, з яких компонентів складається страва, та оціни їхню харчову цінність.
        Правила:
        1. Якщо на фотографії або в тексті немає їжі, поверни isFood = false, порожній dishName і порожній список components.
        2. dishName - коротка назва страви українською.
        3. Кожен продукт - окремий компонент: name - назва українською, grams - маса в грамах (якщо маса не вказана, оціни звичайну порцію), per100g - калорії (ккал), білки, жири й вуглеводи (г) на 100 г цього компонента.
        4. per100g - для продукту в тому вигляді, у якому його їдять: для вареної каші - значення вареної каші, а не сухої крупи.
        5. Калорії мають відповідати 4 ккал на 1 г білків і вуглеводів та 9 ккал на 1 г жирів.
        """;

    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Returns null when the text has no food in it.</summary>
    public Task<EstimateResponse?> EstimateTextAsync(string text, CancellationToken ct) =>
        EstimateAsync($"Оціни з'їдене: {text}", null, ct);

    /// <summary>Returns null when the photo has no food on it.</summary>
    public Task<EstimateResponse?> EstimatePhotoAsync(
        byte[] photo,
        string mediaType,
        CancellationToken ct
    ) =>
        EstimateAsync(
            "Оціни страву на фотографії.",
            $"data:{mediaType};base64,{Convert.ToBase64String(photo)}",
            ct
        );

    /// <summary>Nutrients of each component for its mass, and the total (US-7).</summary>
    public static EstimateResponse Recalculate(RecalculateRequest request)
    {
        var components = request
            .Components.Select(c => new EstimateComponent(
                c.Name.Trim(),
                c.Grams,
                c.Per100g,
                c.Per100g.Scale(c.Grams / 100).Rounded()
            ))
            .ToList();
        return new EstimateResponse(
            request.DishName.Trim(),
            components,
            components.Select(c => c.Nutrients).Sum().Rounded()
        );
    }

    /// <summary>Returns an error message for a wrong estimate, or null.</summary>
    public static string? Validate(EstimateDraft estimate)
    {
        if (!estimate.IsFood)
        {
            return null;
        }
        if (string.IsNullOrWhiteSpace(estimate.DishName) || estimate.Components.Count == 0)
        {
            return "потрібні назва страви і хоча б один компонент";
        }

        foreach (var c in estimate.Components)
        {
            if (string.IsNullOrWhiteSpace(c.Name) || c.Grams <= 0)
            {
                return "кожен компонент має мати назву і масу, більшу за нуль";
            }
            if (c.Per100g.Protein + c.Per100g.Fat + c.Per100g.Carbs > 100)
            {
                return $"у 100 г «{c.Name}» не може бути більше 100 г білків, жирів і вуглеводів разом";
            }
            if (c.Per100g.Validate() is { } error)
            {
                return $"«{c.Name}»: {error}";
            }
        }
        return null;
    }

    /// <summary>"image/jpeg" or "image/png" by the file signature, otherwise null.</summary>
    public static string? DetectImageType(ReadOnlySpan<byte> data) =>
        data.StartsWith(JpegSignature) ? "image/jpeg"
        : data.StartsWith(PngSignature) ? "image/png"
        : null;

    private async Task<EstimateResponse?> EstimateAsync(
        string prompt,
        string? imageDataUrl,
        CancellationToken ct
    )
    {
        var estimate = await openRouter.GenerateAsync<EstimateDraft>(
            options.Value.EstimateModel,
            Instructions,
            prompt,
            imageDataUrl,
            Validate,
            ct
        );
        if (!estimate.IsFood)
        {
            return null;
        }

        var components = estimate
            .Components.Select(c => new ComponentRequest(c.Name, c.Grams, c.Per100g))
            .ToList();
        return Recalculate(new RecalculateRequest(estimate.DishName, components));
    }
}
