using System.Net;
using System.Text.Json;

using FluentAssertions;

using Memoria.Users.Options;
using Memoria.Users.Services;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Memoria.Users.UnitTests.Services;

public sealed class ResendEmailSenderTests
{
    private const string ApiKey = "re_test_key";
    private const string FromAddress = "Memoria <noreply@memoria.test>";

    [Fact]
    public async Task SendVerificationCodePostsExpectedPayloadToResend()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, """{"id":"abc"}""");
        var sut = CreateSut(handler);

        await sut.SendVerificationCodeAsync("user@example.com", "123456", CancellationToken.None);

        var request = handler.LastRequest;
        request.Should().NotBeNull();
        request!.Method.Should().Be(HttpMethod.Post);
        request.RequestUri!.AbsoluteUri.Should().Be("https://api.resend.com/emails");
        request.Headers.Authorization!.Scheme.Should().Be("Bearer");
        request.Headers.Authorization.Parameter.Should().Be(ApiKey);

        var body = JsonDocument.Parse(handler.LastBody!).RootElement;
        body.GetProperty("from").GetString().Should().Be(FromAddress);
        body.GetProperty("to")[0].GetString().Should().Be("user@example.com");
        body.GetProperty("subject").GetString().Should().Contain("Memoria");
        body.GetProperty("html").GetString().Should().Contain("123456");
        body.GetProperty("text").GetString().Should().Contain("123456");
    }

    [Fact]
    public async Task SendVerificationCodeSwallowsHttpErrors()
    {
        var handler = new CapturingHandler(HttpStatusCode.BadGateway, """{"error":"upstream"}""");
        var sut = CreateSut(handler);

        var act = async () => await sut.SendVerificationCodeAsync(
            "user@example.com", "123456", CancellationToken.None);

        await act.Should().NotThrowAsync(
            "fail-open: Resend outages must not surface as 500 to the SPA");
    }

    [Fact]
    public async Task SendVerificationCodeSkipsCallWhenFromAddressMissing()
    {
        var handler = new CapturingHandler(HttpStatusCode.OK, "{}");
        var sut = CreateSut(handler, fromAddress: "");

        await sut.SendVerificationCodeAsync("user@example.com", "123456", CancellationToken.None);

        handler.LastRequest.Should().BeNull("misconfigured sender must not blast Resend with empty From");
    }

    private static ResendEmailSender CreateSut(CapturingHandler handler, string fromAddress = FromAddress)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", ApiKey);

        var options = Microsoft.Extensions.Options.Options.Create(new EmailOptions
        {
            ApiKey = ApiKey,
            FromAddress = fromAddress,
        });

        return new ResendEmailSender(http, options, NullLogger<ResendEmailSender>.Instance);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _responseBody;

        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        public CapturingHandler(HttpStatusCode status, string responseBody)
        {
            _status = status;
            _responseBody = responseBody;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new HttpResponseMessage(_status)
            {
                Content = new StringContent(_responseBody),
            };
        }
    }
}
