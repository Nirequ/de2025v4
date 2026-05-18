using System;
using System.Linq;
using System.Windows;
using Module3.Domain.Models;
using Module3.Domain.Services;

namespace Module3.App.Views;

/// <summary>
/// Рабочее место обычного пользователя. Показывает данные сеанса и
/// позволяет сменить пароль или выйти из системы. Доступ ограничен
/// текущей учётной записью.
/// </summary>
public partial class UserWindow : Window
{
    private readonly int _userId;
    private readonly UserRepository _repo;

    public UserWindow(int userId, UserRepository repo)
    {
        InitializeComponent();
        _userId = userId;
        _repo = repo;
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        var user = _repo.List().FirstOrDefault(u => u.Id == _userId);
        if (user is null) return;
        GreetingText.Text = $"Добро пожаловать, {user.Login}";
        SubtitleText.Text =
            "Вы успешно авторизовались в информационной системе транспортной компании.";
        InfoLogin.Text = $"Логин: {user.Login}";
        InfoRole.Text = $"Роль: {user.Role}";
        InfoLastLogin.Text = user.LastLoginAt is null
            ? "Дата последнего входа: —"
            : $"Дата последнего входа: {user.LastLoginAt:dd.MM.yyyy HH:mm}";
    }

    private void OnChangePasswordClick(object sender, RoutedEventArgs e)
    {
        var dialog = new PasswordChangeWindow(_userId) { Owner = this };
        dialog.ShowDialog();
        Refresh();
    }

    private void OnLogoutClick(object sender, RoutedEventArgs e)
    {
        var login = new LoginWindow();
        login.Show();
        Close();
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (Application.Current.Windows.Count == 0)
            Application.Current.Shutdown();
    }
}
