using SmartBite.Api.Services;
using SmartBite.Contracts;

namespace SmartBite.Tests.Unit;

public class NutrientMathTests
{
    [Fact]
    public void Scale_MultipliesEveryValue()
    {
        // us-5: дві порції дають вдвічі більше
        var twice = new Nutrients(250, 10, 5.5m, 30).Scale(2);

        Assert.Equal(new Nutrients(500, 20, 11, 60), twice);
    }

    [Fact]
    public void Sum_OfNothing_IsZero()
    {
        Assert.Equal(NutrientMath.Zero, Array.Empty<Nutrients>().Sum());
    }

    [Fact]
    public void Sum_AddsEveryValue()
    {
        var total = new[] { new Nutrients(450, 20, 10, 60), new Nutrients(620, 30, 25, 70) }.Sum();

        Assert.Equal(new Nutrients(1070, 50, 35, 130), total);
    }

    [Fact]
    public void Minus_Us8_AcceptanceExample()
    {
        // норма 2080 ккал, записи 450 і 620 ккал - лишається 1010
        var left = new Nutrients(2080, 156, 58, 234).Minus(new Nutrients(1070, 50, 35, 130));

        Assert.Equal(new Nutrients(1010, 106, 23, 104), left);
    }

    [Fact]
    public void Minus_NeverGoesBelowZero()
    {
        // us-8: якщо норму перевищено, залишок 0
        var left = new Nutrients(2000, 100, 50, 200).Minus(new Nutrients(2500, 90, 80, 200));

        Assert.Equal(new Nutrients(0, 10, 0, 0), left);
    }

    [Theory]
    [InlineData(2080.5, 0, 2081)] // середина округлюється від нуля, а не до парного
    [InlineData(2081.5, 0, 2082)]
    [InlineData(57.75, 1, 57.8)]
    [InlineData(57.25, 1, 57.3)]
    [InlineData(0.04, 1, 0)]
    public void Round_UsesAwayFromZero(decimal value, int decimals, decimal expected)
    {
        Assert.Equal(expected, NutrientMath.Round(value, decimals));
    }

    [Fact]
    public void Rounded_KeepsWholeKcalAndOneDecimalForGrams()
    {
        var n = new Nutrients(342.6m, 12.34m, 6.05m, 59.96m).Rounded();

        Assert.Equal(new Nutrients(343, 12.3m, 6.1m, 60), n);
    }

    [Theory]
    [InlineData(342, 12, 6, 60)] // рівно 4/9/4
    [InlineData(380, 12, 6, 60)] // +38 ккал, в межах 15 %
    [InlineData(300, 12, 6, 60)] // -42 ккал, в межах 15 %
    [InlineData(20, 0, 0, 0)] // малі значення, працює допуск 20 ккал
    [InlineData(0, 0, 0, 0)]
    public void Validate_AcceptsConsistentValues(decimal kcal, decimal p, decimal f, decimal c)
    {
        Assert.Null(new Nutrients(kcal, p, f, c).Validate());
    }

    [Theory]
    [InlineData(400, 12, 6, 60)] // +58 ккал, більше ніж 15 % від 342
    [InlineData(100, 12, 6, 60)]
    [InlineData(21, 0, 0, 0)] // трохи більше допуску
    [InlineData(500, 0, 0, 0)] // калорії є, а бжв нема
    public void Validate_RejectsInconsistentCalories(decimal kcal, decimal p, decimal f, decimal c)
    {
        var error = new Nutrients(kcal, p, f, c).Validate();

        Assert.NotNull(error);
        Assert.Contains("не узгоджується", error);
    }

    [Theory]
    [InlineData(-1, 0, 0, 0)]
    [InlineData(100, -1, 0, 25)]
    [InlineData(100, 0, -0.1, 25)]
    [InlineData(100, 25, 0, -1)]
    public void Validate_RejectsNegativeValues(decimal kcal, decimal p, decimal f, decimal c)
    {
        Assert.Equal(
            "значення КБЖВ не можуть бути від'ємними",
            new Nutrients(kcal, p, f, c).Validate()
        );
    }
}
