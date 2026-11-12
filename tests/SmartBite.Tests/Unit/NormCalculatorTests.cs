using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Data;
using SmartBite.Tests.Fakes;

namespace SmartBite.Tests.Unit;

// добова норма за міффліном - сан жеором (docs/03-requirements.md, розділ 6 і us-2)
public class NormCalculatorTests
{
    private static Profile Make(
        Sex sex = Sex.Male,
        int age = 30,
        int height = 180,
        decimal weight = 80,
        ActivityLevel activity = ActivityLevel.Low,
        Goal goal = Goal.Lose
    ) =>
        new()
        {
            Sex = sex,
            Age = age,
            HeightCm = height,
            WeightKg = weight,
            ActivityLevel = activity,
            Goal = goal,
        };

    [Fact]
    public void Us2_AcceptanceExample_Gives2080Kcal()
    {
        // bmr = 800 + 1125 - 150 + 5 = 1780; 1780 * 1.375 * 0.85 = 2080.4
        var norm = NormCalculator.Calculate(Make());

        Assert.Equal(TestData.Us2Norm, norm);
    }

    [Fact]
    public void Female_BmrIs166KcalLowerThanMale()
    {
        var male = NormCalculator.Calculate(
            Make(sex: Sex.Male, activity: ActivityLevel.Sedentary, goal: Goal.Maintain)
        );
        var female = NormCalculator.Calculate(
            Make(sex: Sex.Female, activity: ActivityLevel.Sedentary, goal: Goal.Maintain)
        );

        Assert.Equal(2136, male.Calories); // 1780 * 1.2
        Assert.Equal(1937, female.Calories); // (1780 - 166) * 1.2 = 1936.8
    }

    [Theory]
    [InlineData(ActivityLevel.Sedentary, 2136)] // 1780 * 1.2
    [InlineData(ActivityLevel.Low, 2448)] // * 1.375 = 2447.5, округлення від нуля
    [InlineData(ActivityLevel.Moderate, 2759)] // * 1.55
    [InlineData(ActivityLevel.High, 3071)] // * 1.725 = 3070.5
    [InlineData(ActivityLevel.VeryHigh, 3382)] // * 1.9
    public void ActivityFactor_IsApplied(ActivityLevel activity, decimal expected)
    {
        var norm = NormCalculator.Calculate(Make(activity: activity, goal: Goal.Maintain));

        Assert.Equal(expected, norm.Calories);
    }

    [Theory]
    [InlineData(Goal.Lose, 2080, 156, 58, 234)] // 30 / 25 / 45 %
    [InlineData(Goal.Maintain, 2448, 122, 82, 306)] // 20 / 30 / 50 %
    [InlineData(Goal.Gain, 2692, 168, 75, 337)] // 2447.5 * 1.1 = 2692.25; 25 / 25 / 50 %
    public void Goal_ChangesCaloriesAndMacroShares(
        Goal goal,
        decimal calories,
        decimal protein,
        decimal fat,
        decimal carbs
    )
    {
        var norm = NormCalculator.Calculate(Make(goal: goal));

        Assert.Equal(new Nutrients(calories, protein, fat, carbs), norm);
    }

    [Fact]
    public void MacroCalories_AddUpToTheNorm_WithinRounding()
    {
        foreach (var goal in Enum.GetValues<Goal>())
        foreach (var activity in Enum.GetValues<ActivityLevel>())
        {
            var n = NormCalculator.Calculate(Make(activity: activity, goal: goal));
            var fromMacros = n.Protein * 4 + n.Fat * 9 + n.Carbs * 4;

            // кожне значення в грамах округлене максимум на 0.5 г, тобто 2 + 4.5 + 2 ккал
            Assert.InRange(fromMacros, n.Calories - 8.5m, n.Calories + 8.5m);
        }
    }

    [Fact]
    public void HeavierBody_GivesHigherNorm()
    {
        // us-2: після зміни ваги норма перераховується
        var before = NormCalculator.Calculate(Make(weight: 80));
        var after = NormCalculator.Calculate(Make(weight: 90));

        // 10 кг * 10 * 1.375 * 0.85 = 116.9 ккал
        Assert.Equal(117, after.Calories - before.Calories);
    }

    [Fact]
    public void BoundaryProfile_GivesPositiveNorm()
    {
        // найменші значення, які приймає api: 100 років, 100 см, 30 кг, жінка
        var n = NormCalculator.Calculate(
            Make(
                sex: Sex.Female,
                age: 100,
                height: 100,
                weight: 30,
                goal: Goal.Lose,
                activity: ActivityLevel.Sedentary
            )
        );

        // 300 + 625 - 500 - 161 = 264; 264 * 1.2 * 0.85 = 269.28
        Assert.Equal(269, n.Calories);
        Assert.True(n.Protein > 0 && n.Fat > 0 && n.Carbs > 0);
    }

    [Fact]
    public void UndefinedEnumValue_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NormCalculator.Calculate(Make(activity: (ActivityLevel)42))
        );
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            NormCalculator.Calculate(Make(goal: (Goal)42))
        );
    }
}
