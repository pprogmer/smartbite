using System.Globalization;

namespace SmartBite.Client.ViewModels;

/// <summary>Reading and showing numbers typed by the user (the decimal comma is accepted, as well as the dot).</summary>
public static class Number
{
    public static bool TryParse(string? text, out decimal value) =>
        decimal.TryParse(
            text?.Trim().Replace('.', ','),
            NumberStyles.Number,
            CultureInfo.GetCultureInfo("uk-UA"),
            out value
        );

    /// <summary>"1262,2", "80" - one decimal place at most, none if the value is whole.</summary>
    public static string Show(decimal value, string format = "0.#") =>
        value.ToString(format, CultureInfo.GetCultureInfo("uk-UA"));
}
