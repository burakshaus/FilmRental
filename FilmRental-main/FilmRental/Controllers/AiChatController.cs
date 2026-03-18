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

            var geminiApiKey = _configuration["Gemini:ApiKey"];
            var openRouterApiKey = _configuration["OpenRouter:ApiKey"];
            if (string.IsNullOrEmpty(geminiApiKey) || string.IsNullOrEmpty(openRouterApiKey))
                return StatusCode(500, "API Key (Gemini veya OpenRouter) bulunamadı.");

            // ── RAG Step 1: Retrieve relevant movies via semantic search (Uses OpenRouter Embeddings to match DB) ──
            var movieContext = await RetrieveSemanticMoviesAsync(openRouterApiKey, request.Message);

            // ── RAG Step 2: Build augmented prompt ──
            var systemPrompt = BuildAugmentedPrompt(request.Message, movieContext);

            // ── RAG Step 3: Send to OpenRouter (Qwen Model) Chat ──
            string url = "https://openrouter.ai/api/v1/chat/completions";

            var requestBody = new
            {
                model = "qwen/qwen-2.5-72b-instruct",
                messages = new[]
                {
                    new { role = "user", content = systemPrompt }
                }
            };

            var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

            // OpenRouter expects Authorization Bearer token header
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Add("Authorization", $"Bearer {openRouterApiKey}");
            // OpenRouter optional headers for ranking/analytics
            requestMessage.Headers.Add("HTTP-Referer", "http://localhost:5295"); 
            requestMessage.Headers.Add("X-Title", "FilmRental");
            requestMessage.Content = jsonContent;

            try
            {
                var response = await _httpClient.SendAsync(requestMessage);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return StatusCode((int)response.StatusCode, $"API Hatası: {responseString}");

                using var jsonDoc = JsonDocument.Parse(responseString);
                var root = jsonDoc.RootElement;

                if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
                {
                    var text = choices[0]
                                .GetProperty("message")
                                .GetProperty("content").GetString();

                    return Ok(new { reply = text });
                }

                return StatusCode(500, "OpenRouter (Qwen) API yanıtı beklenmeyen bir formattaydı.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Sunucu hatası: {ex.Message}");
            }
        }

        /// <summary>
        /// Semantic retrieval: gets query embedding and finds top-N movies by cosine similarity.
        /// Falls back to keyword matching if no embeddings exist yet.
        /// </summary>
        private async Task<List<MovieContextItem>> RetrieveSemanticMoviesAsync(string apiKey, string query)
        {
            var queryEmbedding = await GetEmbeddingAsync(apiKey, query);

            var allMovies = await _context.Movies
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieActors).ThenInclude(ma => ma.Actor)
                .ToListAsync();

            // ── Stock filter: exclude movies with no available copies ──
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
                availableMovies = allMovies; // fallback: show all if everything is rented

            // If embeddings exist, use cosine similarity on available movies only
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

            // Fallback: keyword matching
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

        /// <summary>
        /// Calls OpenRouter to get a 768-dim vector (using nomic-ai/nomic-embed-text-v1.5)
        /// </summary>
        private async Task<float[]?> GetEmbeddingAsync(string apiKey, string text)
        {
            var url = "https://openrouter.ai/api/v1/embeddings";
            var body = new
            {
                model = "text-embedding-3-small",
                input = text
            };

            var content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, url);
            requestMessage.Headers.Add("Authorization", $"Bearer {apiKey}");
            requestMessage.Headers.Add("HTTP-Referer", "http://localhost:5295"); 
            requestMessage.Headers.Add("X-Title", "FilmRental");
            requestMessage.Content = content;

            var response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("data", out var dataArr) &&
                dataArr.GetArrayLength() > 0 &&
                dataArr[0].TryGetProperty("embedding", out var values))
            {
                return values.EnumerateArray().Select(v => (float)v.GetDouble()).ToArray();
            }

            return null;
        }

        /// <summary>Calculates cosine similarity between two float vectors.</summary>
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

        /// <summary>Builds the full augmented RAG prompt with database context.</summary>
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
