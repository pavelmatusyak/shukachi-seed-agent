using Refit;

namespace Shukachi.SeedAgent.Api.Services
{
    public interface IEmbeddingServerClient
    {
        [Post("/embed-doc")]
        Task<EmbedResponse> EmbedDocumentAsync(
            [Body] EmbedRequest request,
            CancellationToken cancellationToken = default);

        [Post("/embed-search")]
        Task<EmbedResponse> EmbedSearchAsync(
            [Body] EmbedRequest request,
            CancellationToken cancellationToken = default);
    }

    public sealed class EmbedRequest
    {
        public string? Text { get; set; }
    }

    public sealed class EmbedResponse
    {
        public int Dim { get; set; }
        public float[] Vector { get; set; } = Array.Empty<float>();
    }
}
