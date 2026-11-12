using SmartBite.Api.Services;
using SmartBite.Contracts;

namespace SmartBite.Tests.Fakes;

// дані, які використовують кілька тестів
public static class TestData
{
    // профіль з критерію us-2: норма 2080 ккал, 156 / 58 / 234 г
    public static readonly ProfileRequest Us2Profile = new(
        Sex.Male,
        30,
        180,
        80,
        ActivityLevel.Low,
        Goal.Lose
    );

    public static readonly Nutrients Us2Norm = new(2080, 156, 58, 234);

    // вівсянка: 4 * 12 + 9 * 6 + 4 * 60 = 342 ккал, все сходиться
    public static readonly Nutrients Oats = new(342, 12, 6, 60);

    public static EstimateDraft Porridge() =>
        new(
            true,
            "Вівсянка з бананом",
            [
                new ComponentDraft("Вівсяна каша", 200, new Nutrients(88, 3, 1.7m, 15)),
                new ComponentDraft("Банан", 120, new Nutrients(89, 1.1m, 0.3m, 22.8m)),
            ]
        );

    public static RecipeDraft Omelette(decimal calories = 300) =>
        new(
            "Омлет з помідорами",
            15,
            [new IngredientDraft(1, "Яйця", 120), new IngredientDraft(0, "сіль", 2)],
            ["Збити яйця.", "Підсмажити на олії."],
            // білки і жири підібрані так, щоб 4/9/4 збігалось з калоріями
            new Nutrients(calories, 20, (calories - 80 - 16) / 9, 4)
        );
}
