namespace Module3.Domain.Models;

/// <summary>
/// Роль пользователя в системе. Используются «Администратор» и «Пользователь»
/// (см. требования задания, модуль 3).
/// </summary>
public sealed record Role(int Id, string Name)
{
    public const string Admin = "Администратор";
    public const string User = "Пользователь";
}
