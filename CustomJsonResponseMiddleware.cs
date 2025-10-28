using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace empifisJsonAPI2
{
    public class CustomJsonResponseMiddleware
    {
        private readonly RequestDelegate _next;

        public CustomJsonResponseMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var originalBody = context.Response.Body;
            await using var memory = new MemoryStream();
            context.Response.Body = memory;

            await _next(context);

            memory.Seek(0, SeekOrigin.Begin);
            var responseBody = await new StreamReader(memory, Encoding.UTF8).ReadToEndAsync();

            if (context.Response.ContentType?.ToLower().Contains("application/json") == true && !string.IsNullOrEmpty(responseBody))
            {
                string finalBody;

                var trimmed = responseBody.TrimStart();
                // If raw JSON (starts with { or [), wrap it as a JSON string
                if (trimmed.StartsWith("{") || trimmed.StartsWith("["))
                {
                    // Serialize the raw JSON text as a JSON string so quotes/backslashes are escaped
                    // JsonConvert.SerializeObject will return a quoted string like: "{\"ErrorCode\":0,...}"
                    var serialized = JsonConvert.SerializeObject(responseBody);
                    // Remove outer quotes
                    if (serialized.Length >= 2 && serialized[0] == '"' && serialized[^1] == '"')
                    {
                        var inner = serialized.Substring(1, serialized.Length - 2);
                        // Lowercase any \uXXXX hex digits
                        var processedInner = Regex.Replace(inner, "\\\\u([0-9A-Fa-f]{4})", m => "\\u" + m.Groups[1].Value.ToLowerInvariant());
                        finalBody = '"' + processedInner + '"';
                    }
                    else
                    {
                        // Fallback: just serialize and use as-is
                        finalBody = serialized;
                    }
                }
                else if (responseBody.Length >= 2 && responseBody[0] == '"' && responseBody[^1] == '"')
                {
                    // Already a quoted JSON string. Normalize internal \u escapes to lowercase.
                    var inner = responseBody.Substring(1, responseBody.Length - 2);
                    var processedInner = Regex.Replace(inner, "\\\\u([0-9A-Fa-f]{4})", m => "\\u" + m.Groups[1].Value.ToLowerInvariant());
                    finalBody = '"' + processedInner + '"';
                }
                else
                {
                    // Other content: just lowercase any \uXXXX sequences
                    finalBody = Regex.Replace(responseBody, "\\\\u([0-9A-Fa-f]{4})", m => "\\u" + m.Groups[1].Value.ToLowerInvariant());
                }

                var outBytes = Encoding.UTF8.GetBytes(finalBody);
                context.Response.ContentLength = outBytes.Length;
                await originalBody.WriteAsync(outBytes, 0, outBytes.Length);
            }
            else
            {
                memory.Seek(0, SeekOrigin.Begin);
                await memory.CopyToAsync(originalBody);
            }

            context.Response.Body = originalBody;
        }
    }
}
