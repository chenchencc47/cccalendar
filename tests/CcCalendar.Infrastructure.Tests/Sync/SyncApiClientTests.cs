using System.Net;
using System.Net.Http.Headers;
using System.Text;
using CcCalendar.Core.Sync;
using CcCalendar.Infrastructure.Sync;

namespace CcCalendar.Infrastructure.Tests.Sync;

public sealed class SyncApiClientTests
{
    [Fact]
    public async Task ReadsChangesAfterCursorWithBearerToken()
    {
        Guid workspaceId = Guid.NewGuid();
        Guid entityId = Guid.NewGuid();
        var handler = new RecordingHandler(
            $"{{\"cursor\":7,\"changes\":[{{\"workspaceId\":\"{workspaceId}\",\"entityType\":\"room\",\"entityId\":\"{entityId}\",\"operation\":\"created\",\"version\":7}}]}}");
        var client = new SyncApiClient(
            new HttpClient(handler)
            {
                BaseAddress = new Uri("https://calendar.example.test/"),
            },
            new StaticAccessTokenProvider("access-1"));

        SyncPage page = await client.GetChangesAsync(workspaceId, 3, CancellationToken.None);

        Assert.Equal(7, page.Cursor);
        Assert.Equal(entityId, Assert.Single(page.Changes).EntityId);
        Assert.Equal("3", handler.RequestUri!.Query.Split("cursor=", StringSplitOptions.None)[1]);
        Assert.Equal("Bearer", handler.Authorization!.Scheme);
        Assert.Equal("access-1", handler.Authorization.Parameter);
    }

    private sealed class RecordingHandler(string responseBody) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public AuthenticationHeaderValue? Authorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            Authorization = request.Headers.Authorization;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class StaticAccessTokenProvider(string token) : IAccessTokenProvider
    {
        public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<string?>(token);
        }
    }
}
