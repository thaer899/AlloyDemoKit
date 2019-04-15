using AlloyDemoKit.AI.Models;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace AlloyDemoKit.AI
{
    [RoutePrefix("api/ai-contenteditor")]
    public class ContentEditorApiController : ApiController
    {
        private static readonly Lazy<HttpClient> LazyHttpClient = new Lazy<HttpClient>(() =>
            new HttpClient { BaseAddress = new Uri("http://alloydemokit-gpt-2:5000") });

        [HttpGet]
        [Route("please-finish-my")]
        public async Task<IHttpActionResult> Get(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
            {
                return BadRequest(nameof(sentence));
            }

            var input = sentence.Trim();
            var result = await LazyHttpClient.Value.GetStringAsync($"?input={input}");

            // Use result up to <|endoftext|> (the rest seems random gibberish)
            var cleanResult = result
                .Split(new[] { "<|endoftext|>" }, StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault();

            return Ok(new GeneratedContentResult
            {
                Input = input,
                Result = result,
                CleanResult = $"{input}{cleanResult}"
            });
        }
    }
}
