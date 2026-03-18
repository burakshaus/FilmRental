using DataAccessLayer.Concrete;
using FilmRental.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace FilmRental.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiChatController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Context _context;
        private readonly HttpClient _httpClient;

        // Free models to try in order (if one is rate-limited, try the next)
        private static readonly string[] FreeModels = new[]
        {
            "qwen/qwen3-4b:free",
            "qwen/qwen3-coder:free",
            "google/gemma-3-12b-it:free",
            "meta-llama/llama-3.3-70b-instruct:free",
            "mistralai/mistral-small-3.1-24b-instruct:free",
            "google/gemma-3-27b-it:free"
        };

        public AiChatController(IConfiguration configuration, Context context)
        {
            _configuration = configuration;
            _context = context;
            _httpClient = new HttpClient();
        }

        [HttpPost]
        public async Task<IActionResult> PostMessage([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Mesaj boş olamaz.");

            var openRouterApiKey = _configuration["OpenRouter:ApiKey"];
            if (string.IsNullOrEmpty(openRouterApiKey))
                return StatusCode(500, new { error = "OpenRouter API Key bulunamadı." });

            var geminiApiKey = _configuration["Gemini:ApiKey"];

            // ── RAG Step 1: Retrieve relevant movies ──
            var movieContext = await RetrieveMoviesAsync(geminiApiKey, request.Message);

            // ── RAG Step 2: Build augmented prompt ──
            var systemPrompt = BuildAugmentedPrompt(request.Message, movieContext);

            // ── RAG Step 3: Try each free model until one works ──
            foreach (var model in FreeModels)
            {
                try
                {
                    var reply = await GetOpenRouterResponseAsync(openRouterApiKey, model, systemPrompt);
                    return Ok(new { reply });
                }
                catch (RateLimitException)
                {
                    continue; // Try next model
                }
            }

            return StatusCode(503, new { error = "Tüm ücretsiz AI modelleri şu anda meşgul. Lütfen birkaç saniye sonra tekrar deneyin." });
        }

        /// <summary>
        /// Calls a model via OpenRouter API. OpenAI-compatible format.
        /// </summary>
        private async Task<string> GetOpenRouterResponseAsync(string apiKey, string model, string prompt)
        {
            var requestBody = new
            {
                model = model,
                max_tokens = 1024,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            };

            using var requestMessage = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions");
            requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");
            requestMessage.Headers.Add("HTTP-Referer", "http://localhost:5295");
            requestMessage.Headers.Add("X-Title", "FilmRental");
            requestMessage.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(requestMessage);
            var responseString = await response.Content.ReadAsStringAsync();

            if ((int)response.StatusCode == 429 || responseString.Contains("rate-limited"))
                throw new RateLimitException();

            if (!response.IsSuccessStatusCode)
                throw new Exception($"AI API Hatası ({model}): {responseString}");

            using var jsonDoc = JsonDocument.Parse(responseString);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var content = choices[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";
                
                // Some models wrap responses in <think> tags, strip them
                var thinkEnd = content.IndexOf("</think>");
                if (thinkEnd >= 0)
                    content = content[(thinkEnd + 8)..].Trim();

                return content;
            }

            throw new Exception("API yanıtı beklenmeyen bir formattaydı.");
        }

        /// <summary>
        /// Retrieves relevant movies using keyword matching and optional semantic search.
        /// </summary>
        private async Task<List<MovieContextItem>> RetrieveMoviesAsync(string? geminiApiKey, string query)
        {
            float[]? queryEmbedding = null;
            if (!string.IsNullOrEmpty(geminiApiKey) && geminiApiKey != "(user-secrets ile ayarlanacak)")
            {
                queryEmbedding = await GetEmbeddingAsync(geminiApiKey, query);
            }

            var allMovies = await _context.Movies
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieActors).ThenInclude(ma => ma.Actor)
                .ToListAsync();

            var activeRentalCounts = await _context.Rentals
                .Where(r => r.ReturnedAt == null)
                .GroupBy(r => r.MovieId)
                .Select(g => new { MovieId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MovieId, x => x.Count);

            var availableMovies = allMovies
                .Where(m =>
                {
                    var rented = activeRentalCounts.GetValueOrDefault(m.Id, 0);
                    return m.TotalCopies - rented > 0;
                })
                .ToList();

            if (!availableMovies.Any())
                availableMovies = allMovies;

            var withEmbeddings = availableMovies.Where(m => m.EmbeddingJson != null).ToList();
            if (queryEmbedding != null && withEmbeddings.Count > 5)
            {
                return withEmbeddings
                    .Select(m =>
                    {
                        var vec = JsonSerializer.Deserialize<float[]>(m.EmbeddingJson!)!;
                        return new { Movie = m, Score = CosineSimilarity(queryEmbedding, vec) };
                    })
                    .OrderByDescending(x => x.Score)
                    .Take(8)
                    .Select(x =>
                    {
                        var available = x.Movie.TotalCopies - activeRentalCounts.GetValueOrDefault(x.Movie.Id, 0);
                        return ToContextItem(x.Movie, available);
                    })
                    .ToList();
            }

            var keywords = query.ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w.Length >= 3).ToList();

            var keywordScored = availableMovies
                .Select(m =>
                {
                    var text = $"{m.Title} {m.Overview} {string.Join(" ", m.MovieGenres.Select(mg => mg.Genre.Name))}".ToLower();
                    var score = keywords.Sum(kw => text.Contains(kw) ? 1 : 0) + (m.ImdbRating ?? 0) / 10.0;
                    return new { Movie = m, Score = score };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(8)
                .Select(x =>
                {
                    var available = x.Movie.TotalCopies - activeRentalCounts.GetValueOrDefault(x.Movie.Id, 0);
                    return ToContextItem(x.Movie, available);
                })
                .ToList();

            if (keywordScored.Any()) return keywordScored;

            return availableMovies
                .OrderByDescending(m => m.ImdbRating)
                .Take(8)
                .Select(m =>
                {
                    var available = m.TotalCopies - activeRentalCounts.GetValueOrDefault(m.Id, 0);
                    return ToContextItem(m, available);
                })
                .ToList();
        }

        private async Task<float[]?> GetEmbeddingAsync(string apiKey, string text)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent?key={apiKey}";
            var body = new
            {
                model = "models/gemini-embedding-001",
                content = new { parts = new[] { new { text } } }
            };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("embedding", out var embEl) &&
                embEl.TryGetProperty("values", out var values))
            {
                return values.EnumerateArray().Select(v => (float)v.GetDouble()).ToArray();
            }

            return null;
        }

        private static double CosineSimilarity(float[] a, float[] b)
        {
            if (a.Length != b.Length) return 0;
            double dot = 0, normA = 0, normB = 0;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            if (normA == 0 || normB == 0) return 0;
            return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
        }

        private static MovieContextItem ToContextItem(EntityLayer.Concrete.Movie m, int availableCopies = -1) => new()
        {
            Title = m.Title,
            Overview = m.Overview ?? "Bilgi yok",
            Year = m.ReleaseDate.HasValue ? m.ReleaseDate.Value.Year : 0,
            Rating = m.ImdbRating ?? 0,
            Genres = m.MovieGenres.Select(mg => mg.Genre.Name).ToList(),
            Actors = m.MovieActors.Take(3).Select(ma => ma.Actor.Name).ToList(),
            AvailableCopies = availableCopies
        };

        private static string BuildAugmentedPrompt(string userQuery, List<MovieContextItem> movies)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Sen bir DVD kiralama mağazasının yapay zeka asistanısın.");
            sb.AppendLine("Görevin: Aşağıdaki VERİTABANINDAN çekilen film listesine dayanarak kullanıcının sorusunu yanıtlamak.");
            sb.AppendLine();
            sb.AppendLine("KURALLAR:");
            sb.AppendLine("1. Yalnızca aşağıdaki veritabanı filmlerinden öneri yap.");
            sb.AppendLine("2. Her öneride neden o filmi seçtiğini kısa bir cümleyle açıkla.");
            sb.AppendLine("3. Cevabını emoji'lerle güzelleştir, kısa ve anlaşılır tut.");
            sb.AppendLine("4. Veritabanında uygun film yoksa bunu dürüstçe belirt.");
            sb.AppendLine();
            sb.AppendLine("── VERİTABANINDAKİ İLGİLİ FİLMLER ──");

            for (int i = 0; i < movies.Count; i++)
            {
                var m = movies[i];
                var stockInfo = m.AvailableCopies >= 0 ? $" | Stok: {m.AvailableCopies} kopya" : "";
                sb.AppendLine($"{i + 1}. {m.Title} ({m.Year}) | IMDb: {m.Rating:0.0} | Tür: {string.Join(", ", m.Genres)}{stockInfo}");
                sb.AppendLine($"   Oyuncular: {string.Join(", ", m.Actors)}");
                sb.AppendLine($"   Özet: {(m.Overview.Length > 200 ? m.Overview[..200] + "..." : m.Overview)}");
            }

            sb.AppendLine();
            sb.AppendLine("── KULLANICI SORUSU ──");
            sb.AppendLine(userQuery);
            return sb.ToString();
        }
    }

    /// <summary>Custom exception for rate limiting to trigger model fallback.</summary>
    public class RateLimitException : Exception { }

    public class MovieContextItem
    {
        public string Title { get; set; } = "";
        public string Overview { get; set; } = "";
        public int Year { get; set; }
        public double Rating { get; set; }
        public List<string> Genres { get; set; } = new();
        public List<string> Actors { get; set; } = new();
        public int AvailableCopies { get; set; } = -1;
    }
}
