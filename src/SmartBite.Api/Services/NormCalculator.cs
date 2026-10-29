using SmartBite.Contracts;
using SmartBite.Data;
using static SmartBite.Api.Services.NutrientMath;

namespace SmartBite.Api.Services;

/// <summary>Daily norm by the Mifflin-St Jeor formula (docs/03-requirements.md, section 6).</summary>
public static class NormCalculator
{
    public static Nutrients Calculate(Profile p)
    {
        var bmr = 10 * p.WeightKg + 6.25m * p.HeightCm - 5 * p.Age + (p.Sex == Sex.Male ? 5 : -161);
        var calories = Round(bmr * ActivityFactor(p.ActivityLevel) * GoalFactor(p.Goal));
        var (protein, fat, carbs) = MacroShares(p.Goal);

        return new Nutrients(
            calories,
            Round(calories * protein / KcalPerGramProtein),
            Round(calories * fat / KcalPerGramFat),
            Round(calories * carbs / KcalPerGramCarbs)
        );
    }

    private static decimal ActivityFactor(ActivityLevel level) =>
        level switch
        {
            ActivityLevel.Sedentary => 1.2m,
            ActivityLevel.Low => 1.375m,
            ActivityLevel.Moderate => 1.55m,
            ActivityLevel.High => 1.725m,
            ActivityLevel.VeryHigh => 1.9m,
            _ => throw new ArgumentOutOfRangeException(nameof(level)),
        };

    private static decimal GoalFactor(Goal goal) =>
        goal switch
        {
            Goal.Lose => 0.85m,
            Goal.Maintain => 1.0m,
            Goal.Gain => 1.1m,
            _ => throw new ArgumentOutOfRangeException(nameof(goal)),
        };

    // Shares of calories from protein, fat and carbs.
    private static (decimal Protein, decimal Fat, decimal Carbs) MacroShares(Goal goal) =>
        goal switch
        {
            Goal.Lose => (0.30m, 0.25m, 0.45m),
            Goal.Maintain => (0.20m, 0.30m, 0.50m),
            Goal.Gain => (0.25m, 0.25m, 0.50m),
            _ => throw new ArgumentOutOfRangeException(nameof(goal)),
        };
}
