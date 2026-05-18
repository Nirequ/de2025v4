using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using Module3.Domain.Models;

namespace Module3.Domain.Services;

/// <summary>
/// Слой доступа к данным таблицы «Пользователи» в SQLite. Также гарантирует
/// существование таблиц «Роли» и «Пользователи» (требуется для модуля 3, если
/// БД ещё не была развёрнута модулем 2).
/// </summary>
public sealed class UserRepository
{
    /// <summary>Лимит подряд неудачных попыток входа до блокировки.</summary>
    public const int MaxFailedAttempts = 3;

    /// <summary>Период неактивности (в днях) до автоматической блокировки.</summary>
    public const int InactivityLockoutDays = 30;

    private readonly string _connectionString;

    public UserRepository(string sqliteFilePath)
    {
        var csb = new SqliteConnectionStringBuilder
        {
            DataSource = sqliteFilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
        };
        _connectionString = csb.ToString();
    }

    public string ConnectionString => _connectionString;

    private SqliteConnection Open()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        using (var pragma = conn.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }
        return conn;
    }

    /// <summary>
    /// Создать таблицы (если отсутствуют) и заполнить справочник ролей.
    /// </summary>
    public void EnsureSchema()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Роли (
    id_роли  INTEGER PRIMARY KEY AUTOINCREMENT,
    название TEXT    NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Пользователи (
    id_пользователя           INTEGER PRIMARY KEY AUTOINCREMENT,
    логин                     TEXT    NOT NULL UNIQUE,
    пароль_хеш                TEXT    NOT NULL,
    id_роли                   INTEGER NOT NULL,
    id_сотрудника             INTEGER UNIQUE,
    дата_создания             DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    дата_последнего_входа     DATETIME,
    число_неудачных_попыток   INTEGER NOT NULL DEFAULT 0,
    признак_блокировки        INTEGER NOT NULL DEFAULT 0,
    признак_первого_входа     INTEGER NOT NULL DEFAULT 1,
    FOREIGN KEY (id_роли) REFERENCES Роли(id_роли)
);

INSERT OR IGNORE INTO Роли(название) VALUES ('Администратор');
INSERT OR IGNORE INTO Роли(название) VALUES ('Пользователь');
";
        cmd.ExecuteNonQuery();
    }

    public User? FindByLogin(string login)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT u.id_пользователя, u.логин, u.пароль_хеш, r.название AS роль,
       u.дата_создания, u.дата_последнего_входа,
       u.число_неудачных_попыток, u.признак_блокировки,
       u.признак_первого_входа
FROM Пользователи u
JOIN Роли r ON r.id_роли = u.id_роли
WHERE u.логин = $login";
        cmd.Parameters.AddWithValue("$login", login);
        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapUser(reader) : null;
    }

    public IReadOnlyList<User> List()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT u.id_пользователя, u.логин, u.пароль_хеш, r.название AS роль,
       u.дата_создания, u.дата_последнего_входа,
       u.число_неудачных_попыток, u.признак_блокировки,
       u.признак_первого_входа
FROM Пользователи u
JOIN Роли r ON r.id_роли = u.id_роли
ORDER BY u.логин";
        using var reader = cmd.ExecuteReader();
        var list = new List<User>();
        while (reader.Read())
            list.Add(MapUser(reader));
        return list;
    }

    private int GetRoleId(SqliteConnection conn, string roleName)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id_роли FROM Роли WHERE название = $name";
        cmd.Parameters.AddWithValue("$name", roleName);
        var result = cmd.ExecuteScalar();
        if (result is null || result is DBNull)
            throw new InvalidOperationException(
                $"Роль '{roleName}' не зарегистрирована в БД.");
        return Convert.ToInt32(result);
    }

    /// <summary>
    /// Добавить нового пользователя. Бросает <see cref="InvalidOperationException"/>,
    /// если логин уже занят.
    /// </summary>
    public int Insert(string login, string passwordHash, string roleName)
    {
        using var conn = Open();
        if (LoginExists(conn, login))
            throw new InvalidOperationException(
                $"Пользователь с логином '{login}' уже существует в базе данных.");

        var roleId = GetRoleId(conn, roleName);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO Пользователи
    (логин, пароль_хеш, id_роли, признак_первого_входа)
VALUES ($login, $hash, $role, 1);
SELECT last_insert_rowid();";
        cmd.Parameters.AddWithValue("$login", login);
        cmd.Parameters.AddWithValue("$hash", passwordHash);
        cmd.Parameters.AddWithValue("$role", roleId);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private bool LoginExists(SqliteConnection conn, string login, int? exceptId = null)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = exceptId is null
            ? "SELECT 1 FROM Пользователи WHERE логин = $login"
            : "SELECT 1 FROM Пользователи WHERE логин = $login AND id_пользователя <> $id";
        cmd.Parameters.AddWithValue("$login", login);
        if (exceptId is not null)
            cmd.Parameters.AddWithValue("$id", exceptId.Value);
        return cmd.ExecuteScalar() is not null;
    }

    /// <summary>
    /// Обновить выбранные поля пользователя. Параметры со значением
    /// <see langword="null"/> игнорируются.
    /// </summary>
    public void Update(
        int userId,
        string? login = null,
        string? roleName = null,
        bool? isBlocked = null,
        string? passwordHash = null,
        bool? mustChangePassword = null)
    {
        using var conn = Open();
        var sets = new List<string>();
        var cmd = conn.CreateCommand();
        if (login is not null)
        {
            if (LoginExists(conn, login, userId))
                throw new InvalidOperationException(
                    $"Пользователь с логином '{login}' уже существует в базе данных.");
            sets.Add("логин = $login");
            cmd.Parameters.AddWithValue("$login", login);
        }
        if (roleName is not null)
        {
            sets.Add("id_роли = $role");
            cmd.Parameters.AddWithValue("$role", GetRoleId(conn, roleName));
        }
        if (isBlocked is not null)
        {
            sets.Add("признак_блокировки = $blocked");
            cmd.Parameters.AddWithValue("$blocked", isBlocked.Value ? 1 : 0);
            if (!isBlocked.Value)
                sets.Add("число_неудачных_попыток = 0");
        }
        if (passwordHash is not null)
        {
            sets.Add("пароль_хеш = $hash");
            cmd.Parameters.AddWithValue("$hash", passwordHash);
        }
        if (mustChangePassword is not null)
        {
            sets.Add("признак_первого_входа = $first");
            cmd.Parameters.AddWithValue("$first", mustChangePassword.Value ? 1 : 0);
        }
        if (sets.Count == 0)
            return;
        cmd.CommandText =
            $"UPDATE Пользователи SET {string.Join(", ", sets)} " +
            "WHERE id_пользователя = $id";
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Зафиксировать неудачную попытку входа. Возвращает обновлённое
    /// количество подряд неудачных попыток и признак блокировки.
    /// </summary>
    public (int Attempts, bool BlockedNow) RegisterFailedAttempt(int userId)
    {
        using var conn = Open();
        using var sel = conn.CreateCommand();
        sel.CommandText = "SELECT число_неудачных_попыток FROM Пользователи " +
                          "WHERE id_пользователя = $id";
        sel.Parameters.AddWithValue("$id", userId);
        var current = Convert.ToInt32(sel.ExecuteScalar()) + 1;
        var blocked = current >= MaxFailedAttempts;

        using var upd = conn.CreateCommand();
        upd.CommandText = @"
UPDATE Пользователи
SET число_неудачных_попыток = $cnt,
    признак_блокировки = MAX(признак_блокировки, $blk)
WHERE id_пользователя = $id";
        upd.Parameters.AddWithValue("$cnt", current);
        upd.Parameters.AddWithValue("$blk", blocked ? 1 : 0);
        upd.Parameters.AddWithValue("$id", userId);
        upd.ExecuteNonQuery();
        return (current, blocked);
    }

    /// <summary>
    /// Сбросить счётчик неудачных попыток и обновить дату последнего входа.
    /// </summary>
    public void RegisterSuccessfulLogin(int userId, DateTime? at = null)
    {
        var moment = at ?? DateTime.Now;
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE Пользователи
SET число_неудачных_попыток = 0,
    дата_последнего_входа = $at
WHERE id_пользователя = $id";
        cmd.Parameters.AddWithValue("$at", moment.ToString("o"));
        cmd.Parameters.AddWithValue("$id", userId);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Если пользователь не заходил в систему более <see cref="InactivityLockoutDays"/>,
    /// автоматически устанавливает признак блокировки. Возвращает true, если
    /// блокировка была применена в этом вызове.
    /// </summary>
    public bool ApplyInactivityLockoutIfNeeded(int userId, DateTime? now = null)
    {
        var current = now ?? DateTime.Now;
        using var conn = Open();
        using var sel = conn.CreateCommand();
        sel.CommandText = @"
SELECT дата_последнего_входа, дата_создания, признак_блокировки
FROM Пользователи WHERE id_пользователя = $id";
        sel.Parameters.AddWithValue("$id", userId);
        using var reader = sel.ExecuteReader();
        if (!reader.Read()) return false;
        if (Convert.ToInt32(reader["признак_блокировки"]) == 1)
            return false;
        var referenceObj = reader["дата_последнего_входа"] is DBNull
            ? reader["дата_создания"]
            : reader["дата_последнего_входа"];
        if (referenceObj is DBNull or null) return false;
        if (!DateTime.TryParse(referenceObj.ToString(), out var reference))
            return false;
        if ((current - reference).TotalDays < InactivityLockoutDays) return false;
        reader.Close();

        using var upd = conn.CreateCommand();
        upd.CommandText = "UPDATE Пользователи SET признак_блокировки = 1 " +
                          "WHERE id_пользователя = $id";
        upd.Parameters.AddWithValue("$id", userId);
        upd.ExecuteNonQuery();
        return true;
    }

    public string GetPasswordHash(int userId)
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT пароль_хеш FROM Пользователи " +
                          "WHERE id_пользователя = $id";
        cmd.Parameters.AddWithValue("$id", userId);
        var value = cmd.ExecuteScalar()?.ToString();
        return value ?? throw new InvalidOperationException(
            $"Пользователь id={userId} не найден.");
    }

    public bool HasAdministrator()
    {
        using var conn = Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT COUNT(*)
FROM Пользователи u JOIN Роли r ON r.id_роли = u.id_роли
WHERE r.название = 'Администратор'";
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static User MapUser(SqliteDataReader reader) => new()
    {
        Id = Convert.ToInt32(reader["id_пользователя"]),
        Login = reader["логин"].ToString() ?? string.Empty,
        PasswordHash = reader["пароль_хеш"].ToString() ?? string.Empty,
        Role = reader["роль"].ToString() ?? string.Empty,
        CreatedAt = ParseDateOrDefault(reader["дата_создания"]),
        LastLoginAt = ParseNullableDate(reader["дата_последнего_входа"]),
        FailedAttempts = Convert.ToInt32(reader["число_неудачных_попыток"]),
        IsBlocked = Convert.ToInt32(reader["признак_блокировки"]) == 1,
        MustChangePassword = Convert.ToInt32(reader["признак_первого_входа"]) == 1,
    };

    private static DateTime ParseDateOrDefault(object value)
        => value is DBNull or null
            ? default
            : DateTime.TryParse(value.ToString(), out var d) ? d : default;

    private static DateTime? ParseNullableDate(object value)
        => value is DBNull or null
            ? null
            : DateTime.TryParse(value.ToString(), out var d) ? d : null;
}
