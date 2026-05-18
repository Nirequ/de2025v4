using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Module4.App.Services;
using Module4.Domain.Models;
using Module4.Domain.Services;

namespace Module4.App.Views;

/// <summary>
/// Основное окно приложения «Валидация данных» (см. макет module4.png).
/// Содержит две кнопки — «Получить данные» и «Отправить результат теста» —
/// и два текстовых поля для отображения паспорта и результата проверки.
/// </summary>
public partial class MainWindow : Window
{
    private readonly PassportApiClient _client;
    private readonly TestCaseLogger _testCaseLogger;
    private PassportData? _current;

    public MainWindow()
        : this(CreateDefaultClient(), TestCaseLogger.Default)
    {
    }

    /// <summary>Конструктор для UI-тестов / DI: позволяет передать клиент.</summary>
    public MainWindow(PassportApiClient client, TestCaseLogger logger)
    {
        InitializeComponent();
        _client = client;
        _testCaseLogger = logger;
    }

    private static PassportApiClient CreateDefaultClient()
    {
        var baseUrl = Environment.GetEnvironmentVariable("PASSPORT_API_URL")
            ?? "http://127.0.0.1:5000/";
        if (!baseUrl.EndsWith("/", StringComparison.Ordinal))
            baseUrl += "/";
        return new PassportApiClient(baseUrl);
    }

    private async void OnGetDataClick(object sender, RoutedEventArgs e)
    {
        GetDataButton.IsEnabled = false;
        SendResultButton.IsEnabled = false;
        ResultText.Text = "Получаем данные из эмулятора…";
        ResultText.Style = (Style)FindResource("StatusInfo");
        try
        {
            _current = await _client.GetDataAsync();
            PassportText.Text = _current.FormattedSeriesAndNumber;
            ResultText.Text = "Данные получены. Нажмите «Отправить результат теста».";
            SendResultButton.IsEnabled = true;
        }
        catch (HttpRequestException ex)
        {
            ShowError($"Не удалось получить данные: {ex.Message}");
        }
        catch (Exception ex)
        {
            ShowError($"Неожиданная ошибка: {ex.Message}");
        }
        finally
        {
            GetDataButton.IsEnabled = true;
        }
    }

    private async void OnSendResultClick(object sender, RoutedEventArgs e)
    {
        if (_current is null) return;

        SendResultButton.IsEnabled = false;
        ResultText.Text = "Проверяем данные…";
        ResultText.Style = (Style)FindResource("StatusInfo");

        var outcome = PassportValidator.Validate(_current);
        ShowOutcome(outcome);

        try
        {
            await _client.SetResultAsync(_current, outcome);
            await Task.Run(() => _testCaseLogger.Append(_current, outcome));
        }
        catch (HttpRequestException ex)
        {
            ShowError($"Результат не доставлен на сервер: {ex.Message}");
            return;
        }
        catch (Exception ex)
        {
            ShowError($"Ошибка при отправке результата: {ex.Message}");
            return;
        }

        SendResultButton.IsEnabled = false;
        GetDataButton.IsEnabled = true;
    }

    private void ShowOutcome(ValidationOutcome outcome)
    {
        ResultText.Text = outcome.Message;
        ResultText.Style = (Style)FindResource(outcome.IsValid ? "StatusSuccess" : "StatusError");
    }

    private void ShowError(string message)
    {
        ResultText.Text = message;
        ResultText.Style = (Style)FindResource("StatusError");
    }
}
