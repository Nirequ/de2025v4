using System;

namespace Module3.Domain.Models;

/// <summary>
/// Доменная модель пользователя системы. Отражает поля таблицы
/// "Пользователи" (см. модуль 2, ER-диаграмма).
/// </summary>
public sealed class User
{
    public int Id { get; init; }
    public required string Login { get; set; }
    public required string PasswordHash { get; set; }
    public required string Role { get; set; }
    public DateTime CreatedAt { get; init; }
    public DateTime? LastLoginAt { get; set; }
    public int FailedAttempts { get; set; }
    public bool IsBlocked { get; set; }
    public bool MustChangePassword { get; set; }
}
