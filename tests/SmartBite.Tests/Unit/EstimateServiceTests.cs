using SmartBite.Api.Services;
using SmartBite.Contracts;
using SmartBite.Tests.Fakes;

namespace SmartBite.Tests.Unit;

// перевірка і перерахунок оцінки калорійності (us-6, us-7)
public class EstimateServiceTests
{
    [Fact]
    public void Validate_AcceptsAConsistentEstimate()
    {
        Assert.Null(EstimateService.Validate(TestData.Porridge()));
    }

    [Fact]
    public void Validate_NotFood_IsAcceptedAsIs()
    {
        // модель каже що їжі нема, тоді пусті списки це нормально
        Assert.Null(EstimateService.Validate(new EstimateDraft(false, "", [])));
    }

    [Fact]
    public void Validate_FoodWithoutNameOrComponents_IsRejected()
    {
        Assert.NotNull(EstimateService.Validate(TestData.Porridge() with { DishName = " " }));
        Assert.NotNull(EstimateService.Validate(TestData.Porridge() with { Components = [] }));
    }

    [Theory]
    [InlineData("", 100)]
    [InlineData("Банан", 0)]
    [InlineData("Банан", -50)]
    public void Validate_ComponentWithoutNameOrMass_IsRejected(string name, decimal grams)
    {
        var estimate = TestData.Porridge() with
        {
            Components = [new ComponentDraft(name, grams, new Nutrients(89, 1.1m, 0.3m, 22.8m))],
        };

        Assert.Equal(
            "кожен компонент має мати назву і масу, більшу за нуль",
            EstimateService.Validate(estimate)
        );
    }

    [Fact]
    public void Validate_MoreThan100gOfMacrosIn100g_IsRejected()
    {
        // 4 * 50 + 9 * 40 + 4 * 20 = 640 ккал: сходиться, але так не буває
        var estimate = TestData.Porridge() with
        {
            Components = [new ComponentDraft("Масло", 100, new Nutrients(640, 50, 40, 20))],
        };

        Assert.Contains("не може бути більше 100 г", EstimateService.Validate(estimate));
    }

    [Fact]
    public void Validate_PureFat_IsAccepted()
    {
        // рівно 100 г бжв у 100 г це межа, так можна
        var estimate = TestData.Porridge() with
        {
            Components = [new ComponentDraft("Олія", 10, new Nutrients(900, 0, 100, 0))],
        };

        Assert.Null(EstimateService.Validate(estimate));
    }

    [Fact]
    public void Validate_InconsistentComponent_NamesTheComponent()
    {
        var estimate = TestData.Porridge() with
        {
            Components = [new ComponentDraft("Мед", 10, new Nutrients(900, 0, 0, 80))],
        };

        Assert.StartsWith("«Мед»: калорійність", EstimateService.Validate(estimate));
    }

    [Fact]
    public void Recalculate_ScalesPer100gByMassAndSums()
    {
        var result = EstimateService.Recalculate(
            new RecalculateRequest(
                "  Вівсянка  ",
                [
                    new ComponentRequest(" Каша ", 200, new Nutrients(88, 3, 1.7m, 15)),
                    new ComponentRequest("Банан", 120, new Nutrients(89, 1.1m, 0.3m, 22.8m)),
                ]
            )
        );

        Assert.Equal("Вівсянка", result.DishName);
        Assert.Equal("Каша", result.Components[0].Name);
        Assert.Equal(new Nutrients(176, 6, 3.4m, 30), result.Components[0].Nutrients);
        // 106.8 ккал -> 107; 1.32 г -> 1.3; 0.36 г -> 0.4; 27.36 г -> 27.4
        Assert.Equal(new Nutrients(107, 1.3m, 0.4m, 27.4m), result.Components[1].Nutrients);
        Assert.Equal(new Nutrients(283, 7.3m, 3.8m, 57.4m), result.Total);
    }

    [Fact]
    public void Recalculate_DoublingTheMass_DoublesTheNutrients()
    {
        // us-7: якщо змінити масу зі 150 г на 300 г, кбжв подвоюються
        var per100g = new Nutrients(120, 4, 2, 21);
        Nutrients For(decimal grams) =>
            EstimateService
                .Recalculate(new RecalculateRequest("Страва", [new("Каша", grams, per100g)]))
                .Total;

        Assert.Equal(For(150).Scale(2), For(300));
    }

    [Fact]
    public void DetectImageType_RecognisesJpegAndPngBySignature()
    {
        Assert.Equal("image/jpeg", EstimateService.DetectImageType([0xFF, 0xD8, 0xFF, 0xE0, 1]));
        Assert.Equal(
            "image/png",
            EstimateService.DetectImageType([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0])
        );
    }

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0xFF, 0xD8 })] // обрізаний початок jpeg
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 })] // gif
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })] // pdf
    public void DetectImageType_OtherFiles_AreNull(byte[] data)
    {
        Assert.Null(EstimateService.DetectImageType(data));
    }
}
