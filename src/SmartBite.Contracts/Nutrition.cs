using System.ComponentModel.DataAnnotations;

namespace SmartBite.Contracts;

/// <summary>Calories (kcal) and protein, fat, carbs (g).</summary>
public record Nutrients(
    [Range(0, 100_000, ErrorMessage = "Калорійність має бути від {1} до {2}")] decimal Calories,
    [Range(0, 10_000, ErrorMessage = "Білки мають бути від {1} до {2} г")] decimal Protein,
    [Range(0, 10_000, ErrorMessage = "Жири мають бути від {1} до {2} г")] decimal Fat,
    [Range(0, 10_000, ErrorMessage = "Вуглеводи мають бути від {1} до {2} г")] decimal Carbs
);

public static class EstimateLimits
{
    public const int MaxPhotoBytes = 5 * 1024 * 1024;
}

public record EstimateTextRequest(
    [Required(ErrorMessage = "Введіть, що ви з'їли")]
    [MaxLength(1000, ErrorMessage = "Текст має містити не більше {1} символів")]
        string Text
);

/// <summary>A dish component; <see cref="Nutrients"/> are for <see cref="Grams"/> of it.</summary>
public record EstimateComponent(string Name, decimal Grams, Nutrients Per100g, Nutrients Nutrients);

public record EstimateResponse(
    string DishName,
    IReadOnlyList<EstimateComponent> Components,
    Nutrients Total
);

public record ComponentRequest(
    [Required(ErrorMessage = "Введіть назву компонента")]
    [MaxLength(200, ErrorMessage = "Назва має містити не більше {1} символів")]
        string Name,
    [Range(0d, 10_000d, MinimumIsExclusive = true, ErrorMessage = "Маса має бути більшою за нуль")]
        decimal Grams,
    [Required] Nutrients Per100g
);

/// <summary>An estimate after the user changed component masses or removed components.</summary>
public record RecalculateRequest(
    [Required(ErrorMessage = "Введіть назву страви")]
    [MaxLength(200, ErrorMessage = "Назва має містити не більше {1} символів")]
        string DishName,
    [Required]
    [MinLength(1, ErrorMessage = "Залиште хоча б один компонент")]
        IReadOnlyList<ComponentRequest> Components
);
