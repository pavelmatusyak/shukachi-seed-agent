namespace Shukachi.SeedAgent.Api.Services
{
    public sealed class QdrantOptions
    {
        public string Url { get; set; } = "http://qdrant:6333";
        public string Collection { get; set; } = "messages";
        public int VectorSize { get; set; } = 1;
    }
}
