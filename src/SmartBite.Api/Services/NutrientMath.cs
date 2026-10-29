using SmartBite.Contracts;

namespace SmartBite.Api.Services;

public static class NutrientMath
{
    public const decimal KcalPerGramProtein = 4;
    public const decimal KcalPerGramFat = 9;
    public const decimal KcalPerGramCarbs = 4;

    // Fibre, alcohol and rounding make real calories differ a little from 4/9/4 per gram.
    private const decimal RelativeTolerance = 0.15m;
    private const decimal AbsoluteToleranceKcal = 20;

    public static readonly Nutrients Zero = new(0, 0, 0, 0);

    public static Nutrients Scale(this Nutrients n, decimal factor) =>
        new(n.Calories * factor, n.Protein * factor, n.Fat * factor, n.Carbs * factor);

    public static Nutrients Sum(this IEnumerable<Nutrients> items) =>
        items.Aggregate(
            Zero,
            (a, b) =>
                new(
                    a.Calories + b.Calories,
                    a.Protein + b.Protein,
                    a.Fat + b.Fat,
                    a.Carbs + b.Carbs
                )
        );

    /// <summary>The part of the norm that is left; nothing goes below zero.</summary>
    public static Nutrients Minus(this Nutrients norm, Nutrients consumed) =>
        new(
            Math.Max(0, norm.Calories - consumed.Calories),
            Math.Max(0, norm.Protein - consumed.Protein),
            Math.Max(0, norm.Fat - consumed.Fat),
            Math.Max(0, norm.Carbs - consumed.Carbs)
        );

    /// <summary>Whole kilocalories, grams to one decimal place.</summary>
    public static Nutrients Rounded(this Nutrients n) =>
        new(Round(n.Calories), Round(n.Protein, 1), Round(n.Fat, 1), Round(n.Carbs, 1));

    public static decimal Round(decimal value, int decimals = 0) =>
        Math.Round(value, decimals, MidpointRounding.AwayFromZero);

    /// <summary>Checks values from a language model. Returns an error message or null.</summary>
    public static string? Validate(this Nutrients n)
    {
        if (n.Calories < 0 || n.Protein < 0 || n.Fat < 0 || n.Carbs < 0)
        {
            return "значення КБЖВ не можуть бути від'ємними";
        }

        var expected =
            n.Protein * KcalPerGramProtein + n.Fat * KcalPerGramFat + n.Carbs * KcalPerGramCarbs;
        var tolerance = Math.Max(expected * RelativeTolerance, AbsoluteToleranceKcal);
        return Math.Abs(n.Calories - expected) <= tolerance
            ? null
            : $"калорійність {n.Calories} ккал не узгоджується з білками, жирами та вуглеводами (очікується близько {Round(expected)} ккал)";
    }
}
