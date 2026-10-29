using System.ComponentModel.DataAnnotations;

namespace SmartBite.Contracts;

public record RecipeRequest(
    [Required(ErrorMessage = "Виберіть хоча б один продукт")]
    [MinLength(1, ErrorMessage = "Виберіть хоча б один продукт")]
    [MaxLength(30, ErrorMessage = "Виберіть не більше {1} продуктів")]
        IReadOnlyList<int> ProductIds,
    [Range(1, 10, ErrorMessage = "Кількість порцій має бути від {1} до {2}")] int Servings,
    DateOnly? Date
);

/// <summary>An ingredient with its mass for all servings together.</summary>
public record RecipeIngredient(string Name, decimal Grams);

/// <summary>A generated recipe. <see cref="Remaining"/> is the rest of the daily norm before this dish.</summary>
public record RecipeResponse(
    string Name,
    int CookingTimeMinutes,
    int Servings,
    IReadOnlyList<RecipeIngredient> Ingredients,
    IReadOnlyList<string> Steps,
    Nutrients PerServing,
    Nutrients Remaining
);
