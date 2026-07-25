namespace BronyTV.Models;

public static class PasswordRules
{
    public const int MinLength = 6;

    public static bool TryValidateChange(string? newPassword, string? confirmPassword, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(newPassword) || string.IsNullOrWhiteSpace(confirmPassword))
        {
            error = "Заполните оба поля пароля.";
            return false;
        }

        if (!string.Equals(newPassword, confirmPassword, StringComparison.Ordinal))
        {
            error = "Пароли не совпадают";
            return false;
        }

        if (newPassword.Length < MinLength)
        {
            error = $"Пароль должен содержать минимум {MinLength} символов.";
            return false;
        }

        return true;
    }
}
