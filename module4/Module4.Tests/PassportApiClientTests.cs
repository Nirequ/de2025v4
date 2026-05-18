using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Module4.Domain.Models;
using Module4.Domain.Services;

namespace Module4.Tests;

public class PassportApiClientTests
{
    [Fact]
    public async Task GetDataAsync_ParsesPayload()
    {
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.OK,
            "{\"series\":\"4509\",\"number\":\"638172\",\"issued_at\":\"2014-03-12\",\"comment\":\"ok\"}");

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new PassportApiClient(http);

        var data = await client.GetDataAsync();

        Assert.Equal("4509", data.Series);
        Assert.Equal("638172", data.Number);
        Assert.Equal(new DateOnly(2014, 3, 12), data.IssuedAt);
        Assert.Equal("ok", data.Comment);
        Assert.Equal("get_data", handler.LastRequest!.RequestUri!.AbsolutePath.TrimStart('/'));
        Assert.Equal(HttpMethod.Get, handler.LastRequest.Method);
    }

    [Fact]
    public async Task SetResultAsync_SendsExpectedPayload()
    {
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.OK, "{\"status\":\"accepted\"}");

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new PassportApiClient(http);

        var passport = new PassportData("4509", "638172");
        var outcome = ValidationOutcome.Valid();
        await client.SetResultAsync(passport, outcome);

        Assert.NotNull(handler.LastRequest);
        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Equal("set_result", handler.LastRequest.RequestUri!.AbsolutePath.TrimStart('/'));

        using var doc = JsonDocument.Parse(handler.LastBody!);
        Assert.Equal("4509", doc.RootElement.GetProperty("series").GetString());
        Assert.Equal("638172", doc.RootElement.GetProperty("number").GetString());
        Assert.True(doc.RootElement.GetProperty("is_valid").GetBoolean());
        Assert.Equal(ValidationOutcome.ValidMessage,
            doc.RootElement.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.GetProperty("reasons").ValueKind);
    }

    [Fact]
    public async Task SetResultAsync_ThrowsOnNonSuccess()
    {
        var handler = new StubHandler();
        handler.Enqueue(HttpStatusCode.BadRequest, "{\"error\":\"bad\"}");

        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        using var client = new PassportApiClient(http);

        var passport = new PassportData("0000", "000000");
        var outcome = ValidationOutcome.Invalid("zero");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => client.SetResultAsync(passport, outcome));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly ConcurrentQueue<(HttpStatusCode Status, string Body)> _responses = new();

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public void Enqueue(HttpStatusCode status, string body)
            => _responses.Enqueue((status, body));

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            if (!_responses.TryDequeue(out var pair))
                pair = (HttpStatusCode.NotImplemented, string.Empty);
            return new HttpResponseMessage(pair.Status)
            {
                Content = new StringContent(pair.Body, System.Text.Encoding.UTF8, "application/json"),
            };
        }
    }
}
