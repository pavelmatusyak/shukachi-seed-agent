using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Shukachi.SeedAgent.Api.Services
{
    public sealed class QdrantClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly HttpClient _httpClient;
        private readonly QdrantOptions _options;

        public QdrantClient(HttpClient httpClient, IOptions<QdrantOptions> options)
        {
            _httpClient = httpClient;
            _options = options.Value;
        }

        public async Task StoreMessageAsync(string message, string uid, CancellationToken cancellationToken)
        {
            await EnsureCollectionAsync(cancellationToken);

            var vector = new float[_options.VectorSize];
            var payload = new Dictionary<string, object?>
            {
                ["uid"] = uid,
                ["message"] = message,
                ["created_at_utc"] = DateTimeOffset.UtcNow
            };

            var request = new
            {
                points = new[]
                {
                    new
                    {
                        id = Guid.NewGuid().ToString("N"),
                        vector,
                        payload
                    }
                }
            };

            using var response = await _httpClient.PostAsJsonAsync(
                $"/collections/{_options.Collection}/points",
                request,
                JsonOptions,
                cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
        {
            using var getResponse = await _httpClient.GetAsync(
                $"/collections/{_options.Collection}",
                cancellationToken);

            if (getResponse.IsSuccessStatusCode)
            {
                return;
            }

            if (getResponse.StatusCode != HttpStatusCode.NotFound)
            {
                getResponse.EnsureSuccessStatusCode();
            }

            var createRequest = new
            {
                vectors = new
                {
                    size = _options.VectorSize,
                    distance = "Cosine"
                }
            };

            using var createResponse = await _httpClient.PutAsJsonAsync(
                $"/collections/{_options.Collection}",
                createRequest,
                JsonOptions,
                cancellationToken);
            createResponse.EnsureSuccessStatusCode();
        }
    }
}
