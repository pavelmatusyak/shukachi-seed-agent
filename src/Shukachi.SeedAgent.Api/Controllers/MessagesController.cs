using Microsoft.AspNetCore.Mvc;
using Shukachi.SeedAgent.Api.Services;

namespace Shukachi.SeedAgent.Api.Controllers
{
    [ApiController]
    [Route("messages")]
    public sealed class MessagesController : ControllerBase
    {
        private readonly QdrantClient _qdrantClient;
        private readonly IEmbeddingServerClient _embeddingServerClient;

        public MessagesController(QdrantClient qdrantClient, IEmbeddingServerClient embeddingServerClient)
        {
            _qdrantClient = qdrantClient;
            _embeddingServerClient = embeddingServerClient;
        }

        [HttpGet]
        public async Task<ActionResult<object>> GetMessages(
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _qdrantClient.ScrollMessagesAsync(limit, cancellationToken);
            return Ok(result);
        }

        [HttpPost("search")]
        public async Task<ActionResult<object>> SearchMessages(
            [FromBody] SearchMessagesRequest request,
            CancellationToken cancellationToken = default)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new { error = "query is required" });
            }

            var embed = await _embeddingServerClient.EmbedSearchAsync(
                new EmbedRequest { Text = request.Query },
                cancellationToken);

            if (embed?.Vector == null || embed.Vector.Length == 0)
            {
                return StatusCode(502, new { error = "embedding server returned empty vector" });
            }

            var result = await _qdrantClient.SearchMessagesAsync(
                embed.Vector,
                null,
                request.Limit ?? 5,
                cancellationToken);

            return Ok(result);
        }

        [HttpDelete("collection")]
        public async Task<IActionResult> DeleteMessagesCollection(CancellationToken cancellationToken = default)
        {
            var deleted = await _qdrantClient.DeleteMessagesCollectionAsync(cancellationToken);
            if (!deleted)
            {
                return NotFound(new { error = "collection not found" });
            }

            return NoContent();
        }

        public sealed class SearchMessagesRequest
        {
            public string? Query { get; set; }
            public int? Limit { get; set; }
        }
    }
}
