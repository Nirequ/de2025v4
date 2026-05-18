using System;
using Module3.Domain.Models;

namespace Module3.Domain.Services;

/// <summary>
/// Сервис аутентификации и управления пользователями. Реализует требования
/// модуля 3 демо-экзамена: проверка логина/пароля, блокировка после трёх
/// подряд неверных попыток, автоматическая блокировка при отсутствии
/// активности в течение месяца, форсированная смена пароля при первом входе,
/// добавление/правка/разблокировка пользователей администратором.
/// </summary>
public sealed class AuthService
{
    /// <summary>Сообщение при неверном логине/пароле (из ТЗ).</summary>
    public const string InvalidCredentialsMessage =
        "Вы ввели неверный логин или пароль. " +
        "Пожалуйста проверьте ещё раз введенные данные";

    /// <summary>Сообщение об успешной авторизации (из ТЗ).</summary>
    public const string SuccessMessage = "Вы успешно авторизовались";

    /// <summary>Сообщение о блокировке учётной записи (из ТЗ).</summary>
    public const string BlockedMessage =
        "Вы заблокированы. Обратитесь к администратору";

    /// <summary>Сообщение об успешной смене пароля (из ТЗ).</summary>
    public const string PasswordChangedMessage = "успешной смены пароля";

    /// <summary>Сообщение о дубликате логина (из ТЗ).</summary>
    public const string DuplicateLoginMessage =
        "Пользователь с указанным логином уже существует в базе данных.";

    private readonly UserRepository _repo;

    public AuthService(UserRepository repository)
    {
        _repo = repository;
    }

    /// <summary>
    /// Попытка авторизации по логину и паролю.
    /// </summary>
    /// <param name="login">Логин пользователя (обязателен).</param>
    /// <param name="password">Пароль в открытом виде (обязателен).</param>
    /// <returns>Результат попытки авторизации, см. <see cref="AuthResult"/>.</returns>
    public AuthResult Authenticate(string login, string password)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            return new AuthResult(false,
                "Логин и пароль обязательны для заполнения.");

        var user = _repo.FindByLogin(login);
        if (user is null)
            return new AuthResult(false, InvalidCredentialsMessage);

        if (_repo.ApplyInactivityLockoutIfNeeded(user.Id))
        {
            user.IsBlocked = true;
        }

        if (user.IsBlocked)
            return new AuthResult(false, BlockedMessage, user.Id, user.Role,
                Blocked: true);

        if (!PasswordHasher.Verify(password, user.PasswordHash))
        {
            var (_, blockedNow) = _repo.RegisterFailedAttempt(user.Id);
            return blockedNow
                ? new AuthResult(false, BlockedMessage, user.Id, user.Role,
                    Blocked: true)
                : new AuthResult(false, InvalidCredentialsMessage, user.Id);
        }

        _repo.RegisterSuccessfulLogin(user.Id);
        return new AuthResult(
            true,
            SuccessMessage,
            user.Id,
            user.Role,
            RequirePasswordChange: user.MustChangePassword);
    }

    /// <summary>
    /// Смена пароля. Проверяет текущий пароль и совпадение нового пароля
    /// с подтверждением. Снимает признак «первого входа».
    /// </summary>
    /// <param name="userId">Идентификатор пользователя.</param>
    /// <param name="currentPassword">Текущий пароль.</param>
    /// <param name="newPassword">Новый пароль.</param>
    /// <param name="confirmation">Подтверждение нового пароля.</param>
    public OperationResult ChangePassword(
        int userId,
        string currentPassword,
        string newPassword,
        string confirmation)
    {
        if (string.IsNullOrEmpty(currentPassword)
            || string.IsNullOrEmpty(newPassword)
            || string.IsNullOrEmpty(confirmation))
            return OperationResult.Fail("Все поля обязательны для заполнения.");

        var hash = _repo.GetPasswordHash(userId);
        if (!PasswordHasher.Verify(currentPassword, hash))
            return OperationResult.Fail("Текущий пароль введён неверно.");

        if (!string.Equals(newPassword, confirmation, StringComparison.Ordinal))
            return OperationResult.Fail(
                "Новый пароль и подтверждение не совпадают.");

        if (newPassword.Length < 4)
            return OperationResult.Fail(
                "Длина нового пароля должна быть не меньше 4 символов.");

        if (string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
            return OperationResult.Fail(
                "Новый пароль должен отличаться от текущего.");

        _repo.Update(userId,
            passwordHash: PasswordHasher.Hash(newPassword),
            mustChangePassword: false);
        return OperationResult.Ok($"Сообщение об {PasswordChangedMessage}.");
    }

    /// <summary>
    /// Создать нового пользователя. Используется администратором.
    /// </summary>
    public OperationResult CreateUser(string login, string password, string roleName)
    {
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrEmpty(password))
            return OperationResult.Fail(
                "Поля «Логин» и «Пароль» обязательны для заполнения.");

        try
        {
            _repo.Insert(login, PasswordHasher.Hash(password), roleName);
            return OperationResult.Ok(
                $"Пользователь «{login}» добавлен. Роль: {roleName}.");
        }
        catch (InvalidOperationException)
        {
            return OperationResult.Fail(DuplicateLoginMessage);
        }
    }

    /// <summary>
    /// Обновить данные пользователя (логин, роль, блокировка, сброс пароля).
    /// </summary>
    public OperationResult UpdateUser(
        int userId,
        string? login = null,
        string? roleName = null,
        bool? isBlocked = null,
        string? newPassword = null)
    {
        try
        {
            _repo.Update(
                userId,
                login: login,
                roleName: roleName,
                isBlocked: isBlocked,
                passwordHash: newPassword is null
                    ? null
                    : PasswordHasher.Hash(newPassword),
                mustChangePassword: newPassword is null ? null : true);
            return OperationResult.Ok("Данные пользователя обновлены.");
        }
        catch (InvalidOperationException ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    /// <summary>Снять блокировку с учётной записи.</summary>
    public OperationResult Unblock(int userId) =>
        UpdateUser(userId, isBlocked: false);
}
