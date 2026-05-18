using System;
using System.Linq;
using System.Windows;
using Module3.Domain.Models;
using Module3.Domain.Services;

namespace Module3.App.Views;

/// <summary>
/// Рабочее место администратора. Позволяет вести список пользователей:
/// добавлять новых (с проверкой уникальности логина), изменять данные
/// текущих, снимать блокировку и сбрасывать пароль (с принуждением к смене
/// при следующем входе).
/// </summary>
public partial class AdminWindow : Window
{
    private readonly AuthService _auth;
    private readonly UserRepository _repo;
    private readonly int _currentUserId;

    public AdminWindow(int currentUserId)
    {
        InitializeComponent();
        var app = (App)Application.Current;
        _auth = app.Auth;
        _repo = app.Repository;
        _currentUserId = currentUserId;
        Loaded += (_, _) => Refresh();
    }

    private void Refresh()
    {
        var users = _repo.List();
        UsersGrid.ItemsSource = null;
        UsersGrid.ItemsSource = users;
    }

    private User? GetSelected() => UsersGrid.SelectedItem as User;

    private void OnAddClick(object sender, RoutedEventArgs e)
    {
        var dlg = new UserEditDialog(null, _auth) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            ShowMessage($"Пользователь «{dlg.SavedLogin}» добавлен.", isError: false);
            Refresh();
        }
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        var selected = GetSelected();
        if (selected is null)
        {
            ShowMessage("Выберите пользователя в таблице.", isError: true);
            return;
        }
        var dlg = new UserEditDialog(selected, _auth) { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            ShowMessage($"Данные пользователя «{dlg.SavedLogin}» обновлены.", isError: false);
            Refresh();
        }
    }

    private void OnUnblockClick(object sender, RoutedEventArgs e)
    {
        var selected = GetSelected();
        if (selected is null)
        {
            ShowMessage("Выберите пользователя в таблице.", isError: true);
            return;
        }
        var result = _auth.Unblock(selected.Id);
        ShowMessage(result.Message, isError: !result.Success);
        if (result.Success)
            Refresh();
    }

    private void OnResetPasswordClick(object sender, RoutedEventArgs e)
    {
        var selected = GetSelected();
        if (selected is null)
        {
            ShowMessage("Выберите пользователя в таблице.", isError: true);
            return;
        }
        if (selected.Id == _currentUserId)
        {
            ShowMessage(
                "Используйте окно «Сменить пароль» для собственной учётной записи.",
                isError: true);
            return;
        }
        var newPassword = $"Temp#{DateTime.Now:HHmmss}";
        var result = _auth.UpdateUser(selected.Id, newPassword: newPassword);
        ShowMessage(
            result.Success
                ? $"Пользователю «{selected.Login}» назначен временный пароль: {newPassword}. " +
                  "Он будет вынужден сменить пароль при следующем входе."
                : result.Message,
            isError: !result.Success);
        if (result.Success)
            Refresh();
    }

    private void OnRefreshClick(object sender, RoutedEventArgs e) => Refresh();

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

    private void ShowMessage(string text, bool isError)
    {
        MessageText.Style = (Style)FindResource(isError ? "StatusError" : "StatusSuccess");
        MessageText.Text = text;
    }
}
