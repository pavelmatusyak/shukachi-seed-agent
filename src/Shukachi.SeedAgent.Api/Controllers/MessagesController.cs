using Microsoft.AspNetCore.Mvc;
using Shukachi.SeedAgent.Api.Services;

namespace Shukachi.SeedAgent.Api.Controllers
{
    [ApiController]
    [Route("messages")]
    public sealed class MessagesController : ControllerBase
    {
        private readonly QdrantClient _qdrantClient;

        public MessagesController(QdrantClient qdrantClient)
        {
            _qdrantClient = qdrantClient;
        }

        [HttpGet]
        public async Task<ActionResult<object>> GetMessages(
            [FromQuery] int limit = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _qdrantClient.ScrollMessagesAsync(limit, cancellationToken);
            return Ok(result);
        }
    }
}
