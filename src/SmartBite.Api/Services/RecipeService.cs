using Microsoft.Extensions.Options;
using SmartBite.Contracts;
using SmartBite.Data;

namespace SmartBite.Api.Services;

/// <summary>A recipe as the model returns it.</summary>
public record RecipeDraft(
    string Name,
    int CookingTimeMinutes,
    List<IngredientDraft> Ingredients,
    List<string> Steps,
    Nutrients PerServing
);

/// <param name="ProductId">Id of a selected product, or 0 for a basic ingredient.</param>
public record IngredientDraft(int ProductId, string Name, decimal Grams);

public class RecipeService(OpenRouterClient openRouter, IOptions<OpenRouterOptions> options)
{
    // A serving may exceed the rest of the norm by at most 10 % (US-4).
    public const decimal AllowedExcess = 0.10m;

    // May be used without being selected (US-4).
    public static readonly string[] BasicIngredients = ["сіль", "перець", "вода", "олія"];

    // Stems, so that forms like "солі", "чорний перець", "оливкова олія" are recognised.
    private static readonly string[] BasicStems = ["сіл", "солі", "перец", "перц", "вод", "олі"];

    private static readonly string Instructions = $"""
        Ти кулінарний помічник. Склади один рецепт українською мовою з продуктів користувача.
        Правила:
        1. Використовуй лише продукти зі списку користувача та, за потреби, базові інгредієнти: {string.Join(
            ", ",
            BasicIngredients
        )}. Інших інгредієнтів не додавай.
        2. productId - id продукту зі списку; для базового інгредієнта - 0.
        3. grams - маса інгредієнта в грамах на всі порції разом, не більше, ніж є в наявності.
        4. steps - кроки приготування по порядку, без нумерації.
        5. perServing - калорії (ккал), білки, жири й вуглеводи (г) однієї порції. Калорії мають відповідати 4 ккал на 1 г білків і вуглеводів та 9 ккал на 1 г жирів.
        6. Калорійність однієї порції не має перевищувати залишок добової норми.
        """;

    public async Task<RecipeResponse> GenerateAsync(
        IReadOnlyList<Product> products,
        int servings,
        Nutrients remaining,
        CancellationToken ct
    )
    {
        var prompt = $"""
            Продукти (id: назва, кількість):
            {string.Join("\n", products.Select(p => $"{p.Id}: {p.Name}, {p.Quantity} {p.Unit}"))}
            Кількість порцій: {servings}.
            Залишок добової норми: {remaining.Calories} ккал, білки {remaining.Protein} г, жири {remaining.Fat} г, вуглеводи {remaining.Carbs} г.
            """;
        var maxCalories = NutrientMath.Round(remaining.Calories * (1 + AllowedExcess));

        var recipe = await openRouter.GenerateAsync<RecipeDraft>(
            options.Value.RecipeModel,
            Instructions,
            prompt,
            imageDataUrl: null,
            r => Validate(r, products, maxCalories),
            ct
        );

        return new RecipeResponse(
            recipe.Name.Trim(),
            recipe.CookingTimeMinutes,
            servings,
            recipe
                .Ingredients.Select(i => new RecipeIngredient(
                    i.Name.Trim(),
                    NutrientMath.Round(i.Grams)
                ))
                .ToList(),
            recipe.Steps.Select(s => s.Trim()).ToList(),
            recipe.PerServing.Rounded(),
            remaining
        );
    }

    /// <summary>Returns an error message for a wrong recipe, or null.</summary>
    public static string? Validate(
        RecipeDraft recipe,
        IReadOnlyCollection<Product> products,
        decimal maxCalories
    )
    {
        if (
            string.IsNullOrWhiteSpace(recipe.Name)
            || recipe.Ingredients.Count == 0
            || recipe.Steps.Count == 0
            || recipe.Steps.Any(string.IsNullOrWhiteSpace)
        )
        {
            return "рецепт має містити назву, інгредієнти та кроки приготування";
        }
        if (recipe.CookingTimeMinutes <= 0)
        {
            return "час приготування має бути більшим за нуль";
        }

        foreach (var ingredient in recipe.Ingredients)
        {
            if (ingredient.Grams <= 0)
            {
                return $"маса інгредієнта «{ingredient.Name}» має бути більшою за нуль";
            }

            var allowed =
                ingredient.ProductId == 0
                    ? IsBasic(ingredient.Name)
                    : products.Any(p => p.Id == ingredient.ProductId);
            if (!allowed)
            {
                return $"інгредієнт «{ingredient.Name}» не входить до вибраних продуктів і не є базовим ({string.Join(", ", BasicIngredients)})";
            }
        }

        if (recipe.PerServing.Calories > maxCalories)
        {
            return $"калорійність порції {recipe.PerServing.Calories} ккал перевищує допустимі {maxCalories} ккал";
        }
        return recipe.PerServing.Validate();
    }

    private static bool IsBasic(string name)
    {
        var lower = name.ToLowerInvariant();
        return BasicStems.Any(lower.Contains);
    }
}
