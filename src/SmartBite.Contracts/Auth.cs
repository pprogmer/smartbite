using System.ComponentModel.DataAnnotations;

namespace SmartBite.Contracts;

public record AuthRequest(
    [Required(ErrorMessage = "Введіть адресу електронної пошти")]
    [EmailAddress(ErrorMessage = "Введіть коректну адресу електронної пошти")]
        string Email,
    [Required(ErrorMessage = "Введіть пароль")]
    [MinLength(8, ErrorMessage = "Пароль має містити щонайменше 8 символів")]
        string Password
);

public record AuthResponse(string Token, string Email);
