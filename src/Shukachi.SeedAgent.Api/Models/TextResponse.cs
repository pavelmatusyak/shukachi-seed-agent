namespace Shukachi.SeedAgent.Api.Models
{
    public sealed class TextResponse
    {
        public string Response { get; set; } = string.Empty;
        public string[] References { get; set; } = [];
        public string? Reasoning { get; set; }
    }
}
