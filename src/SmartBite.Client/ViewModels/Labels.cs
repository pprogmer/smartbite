using SmartBite.Contracts;

namespace SmartBite.Client.ViewModels;

/// <summary>A value for a drop-down list with the text shown to the user.</summary>
public record Option<T>(T Value, string Label);

/// <summary>Ukrainian texts for the enums of the API.</summary>
public static class Labels
{
    public static readonly Option<Sex>[] Sexes =
    [
        new(Sex.Male, "Чоловіча"),
        new(Sex.Female, "Жіноча"),
    ];

    public static readonly Option<ActivityLevel>[] Activities =
    [
        new(ActivityLevel.Sedentary, "Мінімальна (сидяча робота, без тренувань)"),
        new(ActivityLevel.Low, "Низька (1–3 тренування на тиждень)"),
        new(ActivityLevel.Moderate, "Середня (3–5 тренувань на тиждень)"),
        new(ActivityLevel.High, "Висока (6–7 тренувань на тиждень)"),
        new(ActivityLevel.VeryHigh, "Дуже висока (фізична праця, щоденні тренування)"),
    ];

    public static readonly Option<Goal>[] Goals =
    [
        new(Goal.Lose, "Зниження маси"),
        new(Goal.Maintain, "Підтримання маси"),
        new(Goal.Gain, "Набір маси"),
    ];

    public static readonly Option<MealType>[] Meals =
    [
        new(MealType.Breakfast, "Сніданок"),
        new(MealType.Lunch, "Обід"),
        new(MealType.Dinner, "Вечеря"),
        new(MealType.Snack, "Перекус"),
    ];

    public static readonly Option<EntrySource>[] Sources =
    [
        new(EntrySource.Manual, "Вручну"),
        new(EntrySource.Recipe, "Рецепт"),
        new(EntrySource.Estimate, "Оцінка"),
    ];

    public static readonly Option<ProductUnit>[] Units =
    [
        new(ProductUnit.Gram, "г"),
        new(ProductUnit.Kilogram, "кг"),
        new(ProductUnit.Milliliter, "мл"),
        new(ProductUnit.Liter, "л"),
        new(ProductUnit.Piece, "шт"),
    ];

    public static readonly Option<ProductCategory>[] Categories =
    [
        new(ProductCategory.Vegetables, "Овочі"),
        new(ProductCategory.Fruits, "Фрукти та ягоди"),
        new(ProductCategory.MeatAndFish, "М'ясо та риба"),
        new(ProductCategory.DairyAndEggs, "Молочні продукти та яйця"),
        new(ProductCategory.Grains, "Крупи, макарони, борошно"),
        new(ProductCategory.Bakery, "Хліб і випічка"),
        new(ProductCategory.Other, "Інше"),
    ];

    public static string Of<T>(this Option<T>[] options, T value)
        where T : notnull => options.First(o => o.Value.Equals(value)).Label;
}
