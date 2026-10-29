using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SmartBite.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<ProductUnit>))]
public enum ProductUnit
{
    Gram,
    Kilogram,
    Milliliter,
    Liter,
    Piece,
}

[JsonConverter(typeof(JsonStringEnumConverter<ProductCategory>))]
public enum ProductCategory
{
    Vegetables,
    Fruits,
    MeatAndFish,
    DairyAndEggs,
    Grains,
    Bakery,
    Other,
}

public record ProductRequest(
    [Required(ErrorMessage = "Введіть назву продукту")]
    [MaxLength(100, ErrorMessage = "Назва має містити не більше {1} символів")]
        string Name,
    [Range(
        0d,
        100_000d,
        MinimumIsExclusive = true,
        ErrorMessage = "Кількість має бути більшою за нуль"
    )]
        decimal Quantity,
    ProductUnit Unit,
    ProductCategory Category
);

public record ProductResponse(
    int Id,
    string Name,
    decimal Quantity,
    ProductUnit Unit,
    ProductCategory Category
);
