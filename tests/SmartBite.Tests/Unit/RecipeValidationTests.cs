using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Data;
using SmartBite.Tests.Fakes;

namespace SmartBite.Tests.Unit;

// перевірка рецепта, який повернула модель (us-4)
public class RecipeValidationTests
{
    private static readonly Product[] Selected =
    [
        new()
        {
            Id = 1,
            Name = "Яйця",
            Quantity = 6,
            Unit = ProductUnit.Piece,
        },
        new()
        {
            Id = 2,
            Name = "Помідори",
            Quantity = 300,
            Unit = ProductUnit.Gram,
        },
    ];

    private static string? Validate(RecipeDraft recipe, decimal maxCalories = 1000) =>
        RecipeService.Validate(recipe, Selected, maxCalories);

    [Fact]
    public void ValidRecipe_Passes()
    {
        Assert.Null(Validate(TestData.Omelette()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyName_IsRejected(string name)
    {
        Assert.NotNull(Validate(TestData.Omelette() with { Name = name }));
    }

    [Fact]
    public void NoIngredientsOrSteps_IsRejected()
    {
        Assert.NotNull(Validate(TestData.Omelette() with { Ingredients = [] }));
        Assert.NotNull(Validate(TestData.Omelette() with { Steps = [] }));
        Assert.NotNull(Validate(TestData.Omelette() with { Steps = ["Збити яйця.", " "] }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveCookingTime_IsRejected(int minutes)
    {
        Assert.Equal(
            "час приготування має бути більшим за нуль",
            Validate(TestData.Omelette() with { CookingTimeMinutes = minutes })
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public void NonPositiveIngredientMass_IsRejected(decimal grams)
    {
        var recipe = TestData.Omelette() with
        {
            Ingredients = [new IngredientDraft(1, "Яйця", grams)],
        };

        Assert.Contains("має бути більшою за нуль", Validate(recipe));
    }

    [Fact]
    public void ProductThatWasNotSelected_IsRejected()
    {
        // id 3 це інший продукт користувача, який не вибрали
        var recipe = TestData.Omelette() with
        {
            Ingredients = [new IngredientDraft(3, "Сир", 50)],
        };

        Assert.Contains("не входить до вибраних продуктів", Validate(recipe));
    }

    [Theory]
    [InlineData("сіль")]
    [InlineData("Сіль")]
    [InlineData("солі")]
    [InlineData("морська сіль")]
    [InlineData("чорний перець")]
    [InlineData("перцю")]
    [InlineData("вода")]
    [InlineData("води")]
    [InlineData("олія")]
    [InlineData("оливкова олія")]
    [InlineData("олії")]
    public void BasicIngredient_IsAllowedWithoutBeingSelected(string name)
    {
        var recipe = TestData.Omelette() with
        {
            Ingredients = [new IngredientDraft(1, "Яйця", 120), new IngredientDraft(0, name, 5)],
        };

        Assert.Null(Validate(recipe));
    }

    [Theory]
    [InlineData("цукор")]
    [InlineData("борошно")]
    // всередині слова є основа базового ("квасолі" містить "солі"), але це не базові інгредієнти
    [InlineData("квасоля")]
    [InlineData("розсолу")]
    public void OtherIngredient_WithoutProductId_IsRejected(string name)
    {
        var recipe = TestData.Omelette() with
        {
            Ingredients = [new IngredientDraft(1, "Яйця", 120), new IngredientDraft(0, name, 5)],
        };

        Assert.Contains("не є базовим", Validate(recipe));
    }

    [Fact]
    public void ServingAboveTheLimit_IsRejected()
    {
        // лишилось 500 ккал, тож можна до 550 (us-4)
        Assert.Null(Validate(TestData.Omelette(550), maxCalories: 550));
        Assert.Contains("перевищує допустимі 550 ккал", Validate(TestData.Omelette(551), 550));
    }

    [Fact]
    public void InconsistentNutrients_AreRejected()
    {
        var recipe = TestData.Omelette() with { PerServing = new Nutrients(900, 10, 5, 10) };

        Assert.Contains("не узгоджується", Validate(recipe));
    }
}
