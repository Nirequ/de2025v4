using System.Windows;
using Module3.Domain.Services;

namespace Module3.App.Views;

/// <summary>
/// Окно смены пароля при первом входе или по запросу администратора.
/// Поля «текущий пароль», «новый пароль», «подтверждение нового пароля»
/// обязательны. Валидация: текущий пароль должен совпадать с хранимым,
/// новый — с подтверждением.
/// </summary>
public partial class PasswordChangeWindow : Window
{
    private readonly AuthService _auth;
    private readonly int _userId;

    public PasswordChangeWindow(int userId)
    {
        InitializeComponent();
        _auth = ((App)Application.Current).Auth;
        _userId = userId;
        Loaded += (_, _) => CurrentInput.Focus();
    }

    private void OnChangeClick(object sender, RoutedEventArgs e)
    {
        var current = CurrentInput.Password ?? string.Empty;
        var next = NewInput.Password ?? string.Empty;
        var confirm = ConfirmInput.Password ?? string.Empty;

        var result = _auth.ChangePassword(_userId, current, next, confirm);
        if (!result.Success)
        {
            ShowMessage(result.Message, isError: true);
            return;
        }

        ShowMessage(result.Message, isError: false);
        MessageBox.Show(
            result.Message,
            "Смена пароля",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowMessage(string text, bool isError)
    {
        MessageText.Style = (Style)FindResource(isError ? "StatusError" : "StatusSuccess");
        MessageText.Text = text;
    }
}
