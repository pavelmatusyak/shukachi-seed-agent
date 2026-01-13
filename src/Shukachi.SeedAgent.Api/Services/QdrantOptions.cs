namespace Shukachi.SeedAgent.Api.Services
{
    public sealed class QdrantOptions
    {
        public string GrpcHost { get; set; } = "qdrant";
        public int GrpcPort { get; set; } = 6334;
        public string Collection { get; set; } = "messages";
        public int VectorSize { get; set; } = 1;
    }
}
