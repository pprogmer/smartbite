using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartBite.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<MealType>))]
public enum MealType
{
    Breakfast,
    Lunch,
    Dinner,
    Snack,
}

[JsonConverter(typeof(JsonStringEnumConverter<EntrySource>))]
public enum EntrySource
{
    Manual,
    Recipe,
    Estimate,
}

public record DiaryEntryRequest(
    DateOnly Date,
    MealType MealType,
    [Required(ErrorMessage = "Введіть назву")]
    [MaxLength(200, ErrorMessage = "Назва має містити не більше {1} символів")]
        string Name,
    EntrySource Source,
    [Range(
        0d,
        100d,
        MinimumIsExclusive = true,
        ErrorMessage = "Кількість порцій має бути більшою за нуль"
    )]
        decimal Servings,
    [Required] Nutrients PerServing
);

public record DiaryEntryResponse(
    int Id,
    DateOnly Date,
    MealType MealType,
    string Name,
    EntrySource Source,
    decimal Servings,
    Nutrients PerServing,
    Nutrients Total
);

/// <summary>A day of the diary. <see cref="Norm"/> and <see cref="Remaining"/> are null until the profile is filled in.</summary>
public record DiaryDayResponse(
    DateOnly Date,
    Nutrients? Norm,
    Nutrients Consumed,
    Nutrients? Remaining,
    decimal ExcessCalories,
    IReadOnlyList<DiaryEntryResponse> Entries
);

public record DayCalories(DateOnly Date, decimal Calories);

public record WeekStatsResponse(decimal? NormCalories, IReadOnlyList<DayCalories> Days);
