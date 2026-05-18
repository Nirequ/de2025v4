using System.Windows;
using Module3.Domain.Models;
using Module3.Domain.Services;

namespace Module3.App.Views;

/// <summary>
/// Диалог добавления / редактирования учётной записи. Используется на
/// рабочем месте администратора. При добавлении проверяет уникальность
/// логина; при редактировании позволяет сменить логин, роль, статус
/// блокировки, а также назначить новый пароль (опционально).
/// </summary>
public partial class UserEditDialog : Window
{
    private readonly AuthService _auth;
    private readonly User? _user;

    public string SavedLogin { get; private set; } = string.Empty;

    public UserEditDialog(User? user, AuthService auth)
    {
        InitializeComponent();
        _user = user;
        _auth = auth;

        RoleInput.Items.Add(Role.Admin);
        RoleInput.Items.Add(Role.User);
        RoleInput.SelectedIndex = 1;

        if (user is null)
        {
            HeaderText.Text = "Новый пользователь";
            SubtitleText.Text = "Заполните логин, роль и пароль и нажмите «Сохранить».";
            PasswordHint.Text = "Пароль обязателен. При первом входе пользователь сменит его.";
            BlockedToggle.IsChecked = false;
        }
        else
        {
            HeaderText.Text = $"Карточка: {user.Login}";
            SubtitleText.Text = "Измените нужные поля и нажмите «Сохранить».";
            LoginInput.Text = user.Login;
            RoleInput.SelectedItem = user.Role;
            BlockedToggle.IsChecked = user.IsBlocked;
        }
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var login = (LoginInput.Text ?? string.Empty).Trim();
        var role = RoleInput.SelectedItem as string ?? Role.User;
        var password = PasswordInput.Password ?? string.Empty;
        var blocked = BlockedToggle.IsChecked == true;

        if (string.IsNullOrEmpty(login))
        {
            ShowError("Логин обязателен для заполнения.");
            return;
        }

        if (_user is null)
        {
            if (string.IsNullOrEmpty(password))
            {
                ShowError("Пароль обязателен для нового пользователя.");
                return;
            }
            var result = _auth.CreateUser(login, password, role);
            if (!result.Success)
            {
                ShowError(result.Message);
                return;
            }
            SavedLogin = login;
            DialogResult = true;
            Close();
            return;
        }

        var update = _auth.UpdateUser(
            _user.Id,
            login: string.Equals(login, _user.Login) ? null : login,
            roleName: role,
            isBlocked: blocked,
            newPassword: string.IsNullOrEmpty(password) ? null : password);
        if (!update.Success)
        {
            ShowError(update.Message);
            return;
        }
        SavedLogin = login;
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string text)
    {
        MessageText.Style = (Style)FindResource("StatusError");
        MessageText.Text = text;
    }
}
