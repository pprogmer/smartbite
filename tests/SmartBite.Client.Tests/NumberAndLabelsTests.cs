using SmartBite.Client.ViewModels;
using SmartBite.Contracts;

namespace SmartBite.Client.Tests;

public class NumberAndLabelsTests
{
    [Theory]
    [InlineData("1,5", 1.5)]
    [InlineData("1.5", 1.5)] // крапка теж підходить, не тільки кома
    [InlineData(" 80 ", 80)]
    [InlineData("0", 0)]
    [InlineData("-2,5", -2.5)] // від'ємні відсіює сервер, а не парсер
    public void TryParse_Accepts(string text, decimal expected)
    {
        Assert.True(Number.TryParse(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("1,2,3")]
    [InlineData("12 г")]
    public void TryParse_Rejects(string? text)
    {
        Assert.False(Number.TryParse(text, out _));
    }

    [Theory]
    [InlineData(80, "80")]
    [InlineData(1262.24, "1262,2")]
    [InlineData(0.5, "0,5")]
    [InlineData(57.75, "57,8")]
    public void Show_UsesOneDecimalAtMost(decimal value, string expected)
    {
        Assert.Equal(expected, Number.Show(value));
    }

    [Fact]
    public void Show_RoundTripsThroughTryParse()
    {
        Assert.True(Number.TryParse(Number.Show(1262.2m), out var value));
        Assert.Equal(1262.2m, value);
    }

    [Fact]
    public void EveryEnumValue_HasExactlyOneUkrainianLabel()
    {
        AssertComplete(Labels.Sexes);
        AssertComplete(Labels.Activities);
        AssertComplete(Labels.Goals);
        AssertComplete(Labels.Meals);
        AssertComplete(Labels.Sources);
        AssertComplete(Labels.Units);
        AssertComplete(Labels.Categories);
    }

    [Fact]
    public void Of_FindsTheLabel()
    {
        Assert.Equal("Обід", Labels.Meals.Of(MealType.Lunch));
        Assert.Equal("шт", Labels.Units.Of(ProductUnit.Piece));
    }

    private static void AssertComplete<T>(Option<T>[] options)
        where T : struct, Enum
    {
        Assert.Equal(Enum.GetValues<T>(), options.Select(o => o.Value));
        Assert.All(options, o => Assert.False(string.IsNullOrWhiteSpace(o.Label)));
        Assert.Equal(options.Length, options.Select(o => o.Label).Distinct().Count());
    }
}
