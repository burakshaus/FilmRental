using BusinessLayer.Abstract;
using DataAccessLayer.Concrete;
using FilmRental.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace FilmRental.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AiChatController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly Context _context;
        private readonly HttpClient _httpClient;
        private readonly ITmdbService _tmdbService;

        // Ücretsiz test için sırayla denenecek farklı yapay zeka modelleri (OpenRouter üzerinden)
        private static readonly string[] FreeModels = new[]
        {
            "mistralai/mistral-small-3.1-24b-instruct:free",
            "google/gemma-3-12b-it:free",
            "google/gemma-3-27b-it:free",
            "qwen/qwen-2.5-72b-instruct",
            "qwen/qwen-2.5-7b-instruct",
            "qwen/qwen-2.5-coder-32b-instruct",
            "meta-llama/llama-3.3-70b-instruct:free"
        };

        public AiChatController(IConfiguration configuration, Context context, ITmdbService tmdbService)
        {
            _configuration = configuration;
            _context = context;
            _httpClient = new HttpClient();
            _tmdbService = tmdbService;
        }

        [HttpPost]
        public async Task<IActionResult> PostMessage([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
                return BadRequest("Mesaj boş olamaz.");

            var openRouterApiKey = _configuration["OpenRouter:ApiKey"];
            if (string.IsNullOrEmpty(openRouterApiKey))
                return StatusCode(500, new { error = "OpenRouter API Key bulunamadı. Lütfen terminalden ayarlayın." });

            var geminiApiKey = _configuration["Gemini:ApiKey"];
            var tmdbApiKey = _configuration["TMDB:ApiKey"];

            int maxAttempts = 5; // Daha da fazla deneme
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                // ── RAG Step 1: İlgili filmleri çek ──
                var movieContext = await RetrieveMoviesAsync(geminiApiKey, request.Message);
                Console.WriteLine($"[DEBUG] Contextual movies found in DB (Attempt {attempt}): {movieContext.Count}");
                foreach(var m in movieContext) 
                    Console.WriteLine($"   - {m.Title} (Rating: {m.Rating}, Stok: {m.AvailableCopies})");

                // ── RAG Step 2: Prompt oluştur ──
                var systemPrompt = BuildAugmentedPrompt(request.Message, movieContext);

                string aiResponse = null;

                // ── RAG Step 3: Modelleri sırayla dene ──
                foreach (var model in FreeModels)
                {
                    try
                    {
                        var reply = await GetOpenRouterResponseAsync(openRouterApiKey, model, systemPrompt);
                        aiResponse = reply;
                        break;
                    }
                    catch (RateLimitException) 
                    {
                        Console.WriteLine($"[WARN] Model {model} is rate limited. Trying next one...");
                        continue; 
                    }
                    catch (Exception ex) 
                    {
                        Console.WriteLine($"[ERROR] Model {model} failed: {ex.Message}. Trying next one...");
                        continue; 
                    }
                }

                if (aiResponse == null)
                    return StatusCode(503, new { error = "Tüm ücretsiz AI modelleri şu anda çok yoğun (Meşgul). Lütfen 1-2 dakika bekleyip tekrar sorunuz." });

                // ── [TMDB_SEARCH: ] KONTROLÜ (AJAN MANTIĞI) ──
                Console.WriteLine($"[DEBUG] AI Response (Attempt {attempt}): {aiResponse}");

                if (aiResponse.Contains("[TMDB_SEARCH:"))
                {
                    if (!string.IsNullOrEmpty(tmdbApiKey))
                    {
                        // Tüm [TMDB_SEARCH: ...] etiketlerini bul
                        var searchTerms = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var searchText = aiResponse;
                        while (searchText.Contains("[TMDB_SEARCH:"))
                        {
                            var startIdx = searchText.IndexOf("[TMDB_SEARCH:") + 13;
                            var endIdx = searchText.IndexOf("]", startIdx);
                            if (endIdx > startIdx)
                            {
                                searchTerms.Add(searchText.Substring(startIdx, endIdx - startIdx).Trim());
                                searchText = searchText[(endIdx + 1)..];
                            }
                            else break;
                        }

                        // Kullanıcının orijinal sorgusunu da TMDB'de ara (seri filmlerin tamamını bulmak için)
                        searchTerms.Add(request.Message.Trim());

                        bool anyAdded = false;
                        foreach (var term in searchTerms)
                        {
                            Console.WriteLine($"[DEBUG] Triggering TMDB Fetch for: '{term}'");
                            var added = await _tmdbService.SearchAndAddMovieAsync(term, tmdbApiKey);
                            Console.WriteLine($"[DEBUG] TmdbService.SearchAndAddMovieAsync('{term}') returned: {added}");
                            if (added) anyAdded = true;
                        }

                        if (anyAdded && attempt < maxAttempts)
                        {
                            Console.WriteLine($"[DEBUG] Movies processed. Re-prompting AI with new database context...");
                            continue;
                        }
                    }

                    return Ok(new { reply = "Üzgünüm, aradığınız filmi dünyada (TMDB) bulamadım veya servislerimiz şu anda yanıt vermiyor." });
                }

                // Normal AI Yanıtıysa dön
                return Ok(new { reply = aiResponse });
            }

            return StatusCode(500, new { error = "Yapay zeka yanıt oluşturamadı." });
        }

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

            if (!response.IsSuccessStatusCode)
            {
                if ((int)response.StatusCode == 429 || responseString.Contains("rate") || responseString.Contains("limit") || responseString.Contains("Too Many Requests"))
                    throw new RateLimitException();
                
                throw new Exception($"HTTP {response.StatusCode}: {responseString}");
            }

            using var jsonDoc = JsonDocument.Parse(responseString);
            var root = jsonDoc.RootElement;

            if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var content = choices[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString() ?? "";
                
                var thinkEnd = content.IndexOf("</think>");
                if (thinkEnd >= 0)
                    content = content[(thinkEnd + 8)..].Trim();

                return content;
            }

            throw new Exception("API yanıtı beklenmeyen bir formattaydı.");
        }

        private async Task<List<MovieContextItem>> RetrieveMoviesAsync(string? geminiApiKey, string query)
        {
            var allMovies = await _context.Movies
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieActors).ThenInclude(ma => ma.Actor)
                .ToListAsync();

            var activeRentalCounts = await _context.Rentals
                .Where(r => r.ReturnedAt == null)
                .GroupBy(r => r.MovieId)
                .Select(g => new { MovieId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.MovieId, x => x.Count);

            // More robust Turkish handling
            string norm(string s) => s.Replace("İ", "i").Replace("I", "ı").Replace("ş", "s").Replace("Ş", "s").Replace("ğ", "g").Replace("Ğ", "g").Replace("ü", "u").Replace("Ü", "u").Replace("ö", "o").Replace("Ö", "o").Replace("ç", "c").Replace("Ç", "c").ToLower();
            
            var queryLower = norm(query);
            var keywords = queryLower.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 3).ToList();

            var scored = allMovies
                .Select(m =>
                {
                    double score = 0;
                    var titleNorm = norm(m.Title);
                    
                    // Exact or Partial Title Match (HUGE BOOST)
                    if (titleNorm == queryLower) score += 500;
                    else if (titleNorm.Contains(queryLower) || queryLower.Contains(titleNorm)) score += 200;

                    // Keyword matches
                    foreach(var kw in keywords) {
                        if (titleNorm.Contains(kw)) score += 50;
                    }

                    // Stock check - include everything, but slightly favor in-stock
                    var rented = activeRentalCounts.GetValueOrDefault(m.Id, 0);
                    var stock = m.TotalCopies - rented;
                    if (stock > 0) score += 5;

                    score += (m.ImdbRating ?? 0) / 10.0;
                    
                    return new { Movie = m, Score = score, Stock = stock };
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(25)
                .Select(x => ToContextItem(x.Movie, x.Stock))
                .ToList();

            if (scored.Count < 5)
            {
                // Fallback to top rated if search didn't yield much
                var fallback = allMovies
                    .OrderByDescending(m => m.ImdbRating)
                    .Take(25)
                    .Select(m => ToContextItem(m, m.TotalCopies - activeRentalCounts.GetValueOrDefault(m.Id, 0)))
                    .ToList();
                
                foreach(var f in fallback) if(!scored.Any(s => s.Title == f.Title)) scored.Add(f);
            }

            return scored.Take(25).ToList();
        }

        private async Task<float[]?> GetEmbeddingAsync(string apiKey, string text)
        {
            try
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
            catch { return null; }
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
            sb.AppendLine("### VERİTABANIMIZDAKİ MEVCUT FİLM LİSTESİ ###");

            if (movies.Count == 0)
            {
                sb.AppendLine("- (Şu an dükkanda hiç film bulunamadı)");
            }
            else
            {
                foreach (var m in movies)
                {
                    var stockInfo = m.AvailableCopies >= 0 ? $" | Stok: {m.AvailableCopies} kopya" : "";
                    sb.AppendLine($"- {m.Title} ({m.Year}) | IMDb: {m.Rating:0.0} | Tür: {string.Join(", ", m.Genres)}{stockInfo}");
                    sb.AppendLine($"  Özet: {m.Overview}");
                }
            }

            sb.AppendLine();
            sb.AppendLine($"### KULLANICI SORUSU: {userQuery} ###");
            sb.AppendLine();
            sb.AppendLine("### ÖNEMLİ TALİMATLAR (KESİNLİKLE UY!) ###");
            sb.AppendLine("1. SADECE yukarıdaki listede olan filmleri önerebilirsin.");
            sb.AppendLine("2. Eğer kullanıcı KESİN BİR FİLM ADI (Örn: 'Recep İvedik', 'Yüzüklerin Efendisi') soruyorsa ve bu film yukarıdaki listede TAM OLARAK YOKSA:");
            sb.AppendLine("   - ASLA açıklama yapma, üzgünüm deme, başka bir şey önerme.");
            sb.AppendLine("   - Sadece şunu yaz: [TMDB_SEARCH: Film Adı]");
            sb.AppendLine("   - Örnek: [TMDB_SEARCH: Titanic]");
            sb.AppendLine("   - ÖNEMLİ: Seri filmler için GENEL seri adını kullan, numara EKLEME. Örnek: [TMDB_SEARCH: Recep İvedik] (DOĞRU), [TMDB_SEARCH: Recep İvedik 3] (YANLIŞ)");
            sb.AppendLine("3. Eğer kullanıcı genel bir şey sorduysa ve uygun film yoksa, o türde ünlü bir film için yine sadece [TMDB_SEARCH: ...] komutunu kullan.");
            sb.AppendLine("4. Eğer listede olan ama 'Stok: 0 kopya' yazan bir filmi (özellikle yeni eklediğimiz filmi) söylüyorsan, kullanıcıya sadece 'Bu film dükkanımızda var ancak şu an stokta bulunmuyor.' de.");

            return sb.ToString();
        }
    }

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
