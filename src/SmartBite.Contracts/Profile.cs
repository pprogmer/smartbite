using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartBite.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<Sex>))]
public enum Sex
{
    Male,
    Female,
}

[JsonConverter(typeof(JsonStringEnumConverter<ActivityLevel>))]
public enum ActivityLevel
{
    Sedentary,
    Low,
    Moderate,
    High,
    VeryHigh,
}

[JsonConverter(typeof(JsonStringEnumConverter<Goal>))]
public enum Goal
{
    Lose,
    Maintain,
    Gain,
}

public record ProfileRequest(
    Sex Sex,
    [Range(14, 100, ErrorMessage = "Вік має бути від {1} до {2} років")] int Age,
    [Range(100, 250, ErrorMessage = "Зріст має бути від {1} до {2} см")] int HeightCm,
    [Range(30, 300, ErrorMessage = "Маса має бути від {1} до {2} кг")] decimal WeightKg,
    ActivityLevel ActivityLevel,
    Goal Goal
);

public record ProfileResponse(
    Sex Sex,
    int Age,
    int HeightCm,
    decimal WeightKg,
    ActivityLevel ActivityLevel,
    Goal Goal,
    Nutrients Norm
);
