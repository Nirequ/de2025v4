using System;
using System.IO;
using System.Windows;
using Module3.App.Views;
using Module3.Domain.Models;
using Module3.Domain.Services;

namespace Module3.App;

/// <summary>
/// Композиционный корень WPF-приложения. Конфигурирует пути к БД,
/// создаёт <see cref="UserRepository"/> и <see cref="AuthService"/>,
/// поднимает окно авторизации.
/// </summary>
public partial class App : Application
{
    private const string DatabaseFileName = "app.db";
    private const string DefaultAdminLogin = "admin";
    private const string DefaultAdminPassword = "Admin#2025";

    public UserRepository Repository { get; private set; } = null!;
    public AuthService Auth { get; private set; } = null!;

    public string DatabasePath { get; private set; } = string.Empty;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            DatabasePath = ResolveDatabasePath();
            Repository = new UserRepository(DatabasePath);
            Repository.EnsureSchema();
            Auth = new AuthService(Repository);

            if (!Repository.HasAdministrator())
            {
                Auth.CreateUser(DefaultAdminLogin, DefaultAdminPassword, Role.Admin);
            }

            var login = new LoginWindow();
            login.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Не удалось инициализировать приложение:" + Environment.NewLine + ex.Message,
                "Ошибка запуска",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    /// <summary>
    /// Использует БД модуля 2, если она существует, иначе создаёт собственную
    /// в папке приложения.
    /// </summary>
    private static string ResolveDatabasePath()
    {
        var sharedDb = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "module2", "app.db"));
        if (File.Exists(sharedDb))
            return sharedDb;
        var localDb = Path.Combine(AppContext.BaseDirectory, DatabaseFileName);
        return localDb;
    }
}
