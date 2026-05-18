namespace Module3.Domain.Models;

/// <summary>
/// Результат попытки авторизации.
/// </summary>
public sealed record AuthResult(
    bool Success,
    string Message,
    int? UserId = null,
    string? Role = null,
    bool RequirePasswordChange = false,
    bool Blocked = false);

/// <summary>
/// Универсальный результат операции (смена пароля, создание пользователя…).
/// </summary>
public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message) => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}
