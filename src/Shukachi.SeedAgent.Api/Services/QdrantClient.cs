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

        public async Task StoreMessageAsync(string message, string uid, CancellationToken cancellationToken)
        {
            await EnsureCollectionAsync(cancellationToken);

            var vector = new Vector();
            vector.Data.AddRange(BuildVector());

            var point = new PointStruct
            {
                Id = new PointId { Uuid = Guid.NewGuid().ToString("D") },
                Vectors = new Vectors { Vector = vector }
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
            await EnsureCollectionAsync(cancellationToken);

            var result = await _client.ScrollAsync(
                _options.Collection,
                limit: (uint)Math.Max(1, limit),
                payloadSelector: true,
                vectorsSelector: false,
                cancellationToken: cancellationToken);

            return result;
        }

        private async Task EnsureCollectionAsync(CancellationToken cancellationToken)
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
                        Size = (uint)_options.VectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: cancellationToken);
            }
        }

        private float[] BuildVector()
        {
            return new float[_options.VectorSize];
        }
    }
}
