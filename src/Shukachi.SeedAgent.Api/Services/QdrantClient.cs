using Microsoft.Extensions.Options;
using Grpc.Core;
using Qdrant.Client.Grpc;
using QdrantSdkClient = Qdrant.Client.QdrantClient;
using QdrantValue = Qdrant.Client.Grpc.Value;

namespace Shukachi.SeedAgent.Api.Services
{
    public sealed class QdrantClient
    {
        private readonly QdrantOptions _options;
        private readonly QdrantSdkClient _client;

        public QdrantClient(IOptions<QdrantOptions> options)
        {
            _options = options.Value;
            _client = new QdrantSdkClient(_options.GrpcHost, _options.GrpcPort);
        }

        public async Task StoreMessageAsync(string message, string uid, IReadOnlyList<float> vector, CancellationToken cancellationToken)
        {
            if (vector == null || vector.Count == 0)
            {
                throw new ArgumentException("Vector must be provided.", nameof(vector));
            }

            await EnsureCollectionAsync(vector.Count, cancellationToken);

            var qdrantVector = new Vector();
            qdrantVector.Data.AddRange(vector);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = Guid.NewGuid().ToString("D") },
                Vectors = new Vectors { Vector = qdrantVector }
            };
            point.Payload.Add("uid", new QdrantValue { StringValue = uid });
            point.Payload.Add("message", new QdrantValue { StringValue = message });
            point.Payload.Add("created_at_utc", new QdrantValue { StringValue = DateTimeOffset.UtcNow.ToString("O") });

            await _client.UpsertAsync(
                _options.Collection,
                new[] { point },
                cancellationToken: cancellationToken);
        }

        public async Task<object> ScrollMessagesAsync(int limit, CancellationToken cancellationToken)
        {
            await EnsureCollectionAsync(_options.VectorSize, cancellationToken);

            var result = await _client.ScrollAsync(
                _options.Collection,
                limit: (uint)Math.Max(1, limit),
                payloadSelector: true,
                vectorsSelector: false,
                cancellationToken: cancellationToken);

            return result;
        }

        public async Task<IReadOnlyList<ScoredPoint>> SearchMessagesAsync(
            IReadOnlyList<float> vector,
            string? uid,
            int limit,
            CancellationToken cancellationToken)
        {
            if (vector == null || vector.Count == 0)
            {
                throw new ArgumentException("Vector must be provided.", nameof(vector));
            }

            await EnsureCollectionAsync(vector.Count, cancellationToken);

            Filter? filter = null;
            if (!string.IsNullOrWhiteSpace(uid))
            {
                filter = Conditions.MatchKeyword("uid", uid);
            }

            var result = await _client.SearchAsync(
                _options.Collection,
                vector.ToArray(),
                filter: filter,
                limit: (ulong)Math.Max(1, limit),
                payloadSelector: true,
                vectorsSelector: false,
                cancellationToken: cancellationToken);

            return result;
        }

        public async Task<bool> DeleteMessagesCollectionAsync(CancellationToken cancellationToken)
        {
            try
            {
                await _client.DeleteCollectionAsync(_options.Collection, cancellationToken: cancellationToken);
                return true;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                return false;
            }
        }

        private async Task EnsureCollectionAsync(int vectorSize, CancellationToken cancellationToken)
        {
            try
            {
                _ = await _client.GetCollectionInfoAsync(_options.Collection, cancellationToken: cancellationToken);
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                await _client.CreateCollectionAsync(
                    _options.Collection,
                    new VectorParams
                    {
                        Size = (uint)vectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);
            }
        }
    }
}
