using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations; // ОБЯЗАТЕЛЬНО ДОБАВЬ ЭТУ СТРОКУ

namespace MixMeShared
{
    public class LoginDto
    {
        [Required(ErrorMessage = "Введите ваш Email")]
        [EmailAddress(ErrorMessage = "Некорректный формат почты")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль")]
        public string Password { get; set; } = string.Empty;
    }

    public class RegisterDto
    {
        [Required(ErrorMessage = "Имя пользователя обязательно")]
        [StringLength(20, MinimumLength = 3, ErrorMessage = "Имя должно быть от 3 до 20 символов")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Только латиница, цифры и подчеркивание")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Введите корректный адрес почты")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Пароль обязателен")]
        [MinLength(6, ErrorMessage = "Минимум 6 символов")]
        public string Password { get; set; } = string.Empty;

        public string Role { get; set; } = "customer";

        public string? FullName { get; set; }
    }
}