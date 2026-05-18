using System;
using System.Windows;
using System.Windows.Input;
using Module3.Domain.Models;
using Module3.Domain.Services;

namespace Module3.App.Views;

/// <summary>
/// Окно авторизации. Поля «Логин» и «Пароль» обязательны для заполнения.
/// При успешной авторизации показывает сообщение «Вы успешно авторизовались»,
/// затем открывает форму смены пароля (если требуется) или рабочее место
/// в зависимости от роли. Блокирует ввод после блокировки учётной записи.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly AuthService _auth;
    private readonly UserRepository _repo;

    public LoginWindow()
    {
        InitializeComponent();
        var app = (App)Application.Current;
        _auth = app.Auth;
        _repo = app.Repository;
        Loaded += (_, _) => LoginInput.Focus();
    }

    private void OnInputKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnSignInClick(sender, new RoutedEventArgs());
        }
    }

    private void OnSignInClick(object sender, RoutedEventArgs e)
    {
        var login = LoginInput.Text?.Trim() ?? string.Empty;
        var password = PasswordInput.Password ?? string.Empty;

        if (string.IsNullOrEmpty(login) || string.IsNullOrEmpty(password))
        {
            ShowError("Поля «Логин» и «Пароль» обязательны для заполнения.");
            return;
        }

        SignInButton.IsEnabled = false;
        try
        {
            var result = _auth.Authenticate(login, password);
            if (!result.Success)
            {
                ShowError(result.Message);
                if (result.Blocked)
                {
                    PasswordInput.Clear();
                }
                else
                {
                    PasswordInput.SelectAll();
                    PasswordInput.Focus();
                }
                return;
            }

            ShowSuccess(AuthService.SuccessMessage);
            MessageBox.Show(
                AuthService.SuccessMessage,
                "Авторизация",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            if (result.RequirePasswordChange)
            {
                var dialog = new PasswordChangeWindow(result.UserId!.Value)
                {
                    Owner = this
                };
                var changed = dialog.ShowDialog();
                if (changed != true)
                    return;
            }

            OpenDesktopFor(result.UserId!.Value, result.Role ?? Role.User);
            Close();
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private void OpenDesktopFor(int userId, string role)
    {
        Window next = role switch
        {
            Role.Admin => new AdminWindow(userId),
            _ => new UserWindow(userId, _repo),
        };
        next.Show();
    }

    private void ShowError(string message)
    {
        MessageText.Style = (Style)FindResource("StatusError");
        MessageText.Text = message;
    }

    private void ShowSuccess(string message)
    {
        MessageText.Style = (Style)FindResource("StatusSuccess");
        MessageText.Text = message;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        if (Application.Current.Windows.Count == 0)
            Application.Current.Shutdown();
    }
}
