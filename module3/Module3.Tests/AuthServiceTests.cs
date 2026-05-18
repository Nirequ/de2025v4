using System;
using System.IO;
using Module3.Domain.Models;
using Module3.Domain.Services;

namespace Module3.Tests;

/// <summary>
/// Сквозные тесты <see cref="AuthService"/> поверх временной SQLite БД.
/// </summary>
public class AuthServiceTests : IDisposable
{
    private readonly string _dbPath;
    private readonly UserRepository _repo;
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(),
            $"module3-tests-{Guid.NewGuid():N}.db");
        _repo = new UserRepository(_dbPath);
        _repo.EnsureSchema();
        _auth = new AuthService(_repo);
        _auth.CreateUser("alice", "Hello123", Role.User);
        _auth.CreateUser("bob", "Bob#2025", Role.Admin);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { File.Delete(_dbPath); } catch { }
    }

    [Fact]
    public void Authenticate_Success_ReturnsSuccessAndRole()
    {
        var result = _auth.Authenticate("alice", "Hello123");
        Assert.True(result.Success);
        Assert.Equal(AuthService.SuccessMessage, result.Message);
        Assert.Equal(Role.User, result.Role);
        Assert.True(result.RequirePasswordChange,
            "Новый пользователь должен сменить пароль при первом входе.");
    }

    [Fact]
    public void Authenticate_EmptyInputs_ReturnsValidationError()
    {
        var r1 = _auth.Authenticate("", "x");
        var r2 = _auth.Authenticate("alice", "");
        Assert.False(r1.Success);
        Assert.False(r2.Success);
    }

    [Fact]
    public void Authenticate_UnknownLogin_ReturnsExamMessage()
    {
        var result = _auth.Authenticate("ghost", "anything");
        Assert.False(result.Success);
        Assert.Equal(AuthService.InvalidCredentialsMessage, result.Message);
    }

    [Fact]
    public void Authenticate_ThreeFailures_BlockAccount()
    {
        _auth.Authenticate("alice", "wrong1");
        _auth.Authenticate("alice", "wrong2");
        var thirdAttempt = _auth.Authenticate("alice", "wrong3");
        Assert.True(thirdAttempt.Blocked);
        Assert.Equal(AuthService.BlockedMessage, thirdAttempt.Message);

        var afterBlock = _auth.Authenticate("alice", "Hello123");
        Assert.False(afterBlock.Success);
        Assert.True(afterBlock.Blocked);
        Assert.Equal(AuthService.BlockedMessage, afterBlock.Message);
    }

    [Fact]
    public void Authenticate_BlockedThenUnblocked_AllowsLogin()
    {
        _auth.Authenticate("alice", "x");
        _auth.Authenticate("alice", "x");
        _auth.Authenticate("alice", "x");

        var bob = _repo.FindByLogin("bob")!;
        var alice = _repo.FindByLogin("alice")!;
        Assert.True(alice.IsBlocked);

        _auth.Unblock(alice.Id);
        var result = _auth.Authenticate("alice", "Hello123");
        Assert.True(result.Success);
        Assert.NotNull(bob);
    }

    [Fact]
    public void Authenticate_InactivityOverThirtyDays_LocksAccount()
    {
        var alice = _repo.FindByLogin("alice")!;
        _repo.RegisterSuccessfulLogin(alice.Id, DateTime.Now.AddDays(-31));
        var locked = _repo.ApplyInactivityLockoutIfNeeded(alice.Id);
        Assert.True(locked);
        var after = _auth.Authenticate("alice", "Hello123");
        Assert.False(after.Success);
        Assert.True(after.Blocked);
    }

    [Fact]
    public void ChangePassword_RequiresCorrectCurrent()
    {
        var alice = _repo.FindByLogin("alice")!;
        var bad = _auth.ChangePassword(alice.Id, "Wrong", "NewPass1", "NewPass1");
        Assert.False(bad.Success);
        Assert.Contains("Текущий пароль", bad.Message);
    }

    [Fact]
    public void ChangePassword_RequiresMatchingConfirmation()
    {
        var alice = _repo.FindByLogin("alice")!;
        var bad = _auth.ChangePassword(alice.Id, "Hello123", "NewPass1", "Mismatch1");
        Assert.False(bad.Success);
    }

    [Fact]
    public void ChangePassword_HappyPath_ClearsMustChangeFlag()
    {
        var alice = _repo.FindByLogin("alice")!;
        var ok = _auth.ChangePassword(alice.Id, "Hello123", "NewPass1", "NewPass1");
        Assert.True(ok.Success);
        Assert.Contains("успешной смены пароля", ok.Message);

        var loginResult = _auth.Authenticate("alice", "NewPass1");
        Assert.True(loginResult.Success);
        Assert.False(loginResult.RequirePasswordChange);
    }

    [Fact]
    public void CreateUser_DuplicateLogin_ReturnsExamMessage()
    {
        var first = _auth.CreateUser("eve", "Pass#123", Role.User);
        Assert.True(first.Success);
        var second = _auth.CreateUser("eve", "Pass#456", Role.User);
        Assert.False(second.Success);
        Assert.Equal(AuthService.DuplicateLoginMessage, second.Message);
    }

    [Fact]
    public void UpdateUser_ChangesRoleAndUnblock()
    {
        var alice = _repo.FindByLogin("alice")!;
        _auth.UpdateUser(alice.Id, isBlocked: true);
        var update = _auth.UpdateUser(
            alice.Id,
            roleName: Role.Admin,
            isBlocked: false);
        Assert.True(update.Success);
        var refreshed = _repo.FindByLogin("alice")!;
        Assert.Equal(Role.Admin, refreshed.Role);
        Assert.False(refreshed.IsBlocked);
        Assert.Equal(0, refreshed.FailedAttempts);
    }
}
