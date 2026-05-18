using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Module4.Domain.Models;

namespace Module4.Domain.Services;

/// <summary>
/// HTTP-клиент для эмулятора отправки паспортных данных, описанного в
/// <c>module4/api/server.py</c>. Реализует методы <c>GET /get_data</c>
/// и <c>POST /set_result</c>.
/// </summary>
public sealed class PassportApiClient : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    public PassportApiClient(string baseUrl)
        : this(new HttpClient { BaseAddress = new Uri(baseUrl) }, ownsClient: true)
    {
    }

    public PassportApiClient(HttpClient client, bool ownsClient = false)
    {
        ArgumentNullException.ThrowIfNull(client);
        _http = client;
        _ownsClient = ownsClient;
    }

    /// <summary>
    /// Запрашивает у эмулятора очередной паспорт через <c>GET /get_data</c>.
    /// </summary>
    public async Task<PassportData> GetDataAsync(CancellationToken ct = default)
    {
        var dto = await _http.GetFromJsonAsync<PassportDto>("get_data", JsonOptions, ct)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                "Эмулятор вернул пустой ответ на GET /get_data.");
        return dto.ToPassportData();
    }

    /// <summary>
    /// Отправляет результат проверки через <c>POST /set_result</c>.
    /// </summary>
    public async Task SetResultAsync(
        PassportData passport,
        ValidationOutcome outcome,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(passport);
        ArgumentNullException.ThrowIfNull(outcome);

        var payload = new ResultDto(
            Series: passport.Series,
            Number: passport.Number,
            IsValid: outcome.IsValid,
            Message: outcome.Message,
            Reasons: outcome.Reasons);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http
            .PostAsync("set_result", content, ct)
            .ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Эмулятор отказался принимать результат ({(int)response.StatusCode}): {body}");
        }
    }

    public void Dispose()
    {
        if (_ownsClient)
            _http.Dispose();
    }

    private sealed record PassportDto(
        [property: JsonPropertyName("series")] string? Series,
        [property: JsonPropertyName("number")] string? Number,
        [property: JsonPropertyName("issued_at")] string? IssuedAt,
        [property: JsonPropertyName("comment")] string? Comment)
    {
        public PassportData ToPassportData()
        {
            DateOnly? issued = null;
            if (!string.IsNullOrWhiteSpace(IssuedAt) &&
                DateOnly.TryParse(IssuedAt, out var parsed))
            {
                issued = parsed;
            }
            return new PassportData(
                Series: Series ?? string.Empty,
                Number: Number ?? string.Empty,
                IssuedAt: issued,
                Comment: Comment);
        }
    }

    private sealed record ResultDto(
        [property: JsonPropertyName("series")] string Series,
        [property: JsonPropertyName("number")] string Number,
        [property: JsonPropertyName("is_valid")] bool IsValid,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("reasons")] System.Collections.Generic.IReadOnlyList<string> Reasons);
}
