using DataAccessLayer.Concrete;
using EntityLayer.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;


namespace FilmRental.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SeedController : ControllerBase
    {
        private readonly Context _context;
        private readonly IConfiguration _configuration;
        private readonly HttpClient _httpClient;

        public SeedController(Context context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        [HttpPost("RunTmdbSeed")]
        public async Task<IActionResult> RunTmdbSeed([FromQuery] int pagesToFetch = 5)
        {
            var apiKey = _configuration["TMDB:ApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_TMDB_API_KEY_HERE")
            {
                return BadRequest("TMDB API Key eksik. Lütfen appsettings.json dosyasını güncelleyin.");
            }

            int addedMoviesCount = 0;
            
            try
            {
                // Türleri (Genres) Baştan Çek ve Eşleştir
                var genreMap = await FetchAndSeedGenresAsync(apiKey);

                for (int page = 1; page <= pagesToFetch; page++)
                {
                    var tmdbUrl = $"https://api.themoviedb.org/3/movie/popular?api_key={apiKey}&language=tr-TR&page={page}";
                    var response = await _httpClient.GetAsync(tmdbUrl);
                    
                    if (!response.IsSuccessStatusCode) continue;

                    var jsonString = await response.Content.ReadAsStringAsync();
                    using var jsonDoc = JsonDocument.Parse(jsonString);
                    var results = jsonDoc.RootElement.GetProperty("results");

                    foreach (var m in results.EnumerateArray())
                    {
                        var tmdbId = m.GetProperty("id").GetInt32();
                        var title = m.GetProperty("title").GetString();
                        
                        // Film zaten varsa atla
                        if (_context.Movies.Any(x => x.Title == title)) continue;

                        var overview = m.GetProperty("overview").GetString();
                        var posterPath = m.TryGetProperty("poster_path", out var pPath) && pPath.ValueKind == JsonValueKind.String ? pPath.GetString() : null;
                        var releaseDateStr = m.TryGetProperty("release_date", out var rDate) && rDate.ValueKind == JsonValueKind.String ? rDate.GetString() : null;
                        var rating = m.TryGetProperty("vote_average", out var vAvg) && vAvg.ValueKind == JsonValueKind.Number ? vAvg.GetDouble() : 0;

                        DateTime? releaseDate = null;
                        if (!string.IsNullOrEmpty(releaseDateStr) && DateTime.TryParse(releaseDateStr, out var parsedDate))
                        {
                            releaseDate = parsedDate;
                        }

                        var newMovie = new Movie
                        {
                            Title = title,
                            Overview = overview,
                            PosterPath = posterPath,
                            ImdbRating = rating,
                            ReleaseDate = releaseDate
                        };

                        _context.Movies.Add(newMovie);
                        await _context.SaveChangesAsync(); // Get ID
                        addedMoviesCount++;

                        // Film Türlerini Eşleştir
                        if (m.TryGetProperty("genre_ids", out var genreIds))
                        {
                            foreach (var gId in genreIds.EnumerateArray())
                            {
                                int tmdbGenreId = gId.GetInt32();
                                if (genreMap.TryGetValue(tmdbGenreId, out int localGenreId))
                                {
                                    _context.MovieGenres.Add(new MovieGenre
                                    {
                                        MovieId = newMovie.Id,
                                        GenreId = localGenreId
                                    });
                                }
                            }
                        }

                        // Oyuncuları Çek
                        await FetchAndSeedCreditsAsync(apiKey, tmdbId, newMovie.Id);
                    }
                    
                    await _context.SaveChangesAsync();
                    
                    // TMDB Hız Sınırını aşmamak için kısa bekleme
                    await Task.Delay(200); 
                }

                return Ok(new { message = $"Seed işlemi tamamlandı. Toplam {addedMoviesCount} yeni film eklendi!" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Bir hata oluştu: {ex.Message}");
            }
        }

        private async Task<Dictionary<int, int>> FetchAndSeedGenresAsync(string apiKey)
        {
            var map = new Dictionary<int, int>();
            var url = $"https://api.themoviedb.org/3/genre/movie/list?api_key={apiKey}&language=tr-TR";
            
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return map;

            var jsonString = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var genres = jsonDoc.RootElement.GetProperty("genres");

            foreach (var g in genres.EnumerateArray())
            {
                int tmdbId = g.GetProperty("id").GetInt32();
                string name = g.GetProperty("name").GetString();

                var existingGenre = _context.Genres.FirstOrDefault(x => x.Name == name);
                if (existingGenre == null)
                {
                    existingGenre = new Genre { Name = name };
                    _context.Genres.Add(existingGenre);
                    await _context.SaveChangesAsync();
                }

                map[tmdbId] = existingGenre.Id;
            }

            return map;
        }

        private async Task FetchAndSeedCreditsAsync(string apiKey, int tmdbMovieId, int localMovieId)
        {
            var url = $"https://api.themoviedb.org/3/movie/{tmdbMovieId}/credits?api_key={apiKey}";
            var response = await _httpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode) return;

            var jsonString = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var cast = jsonDoc.RootElement.GetProperty("cast");

            // Sadece ilk 5 oyuncuyu alalım (Veritabanını aşırı şişirmemek için)
            int count = 0;
            foreach (var c in cast.EnumerateArray())
            {
                if (count >= 5) break;

                string name = c.GetProperty("name").GetString();
                string character = c.GetProperty("character").GetString();
                string profilePath = c.TryGetProperty("profile_path", out var pPath) && pPath.ValueKind == JsonValueKind.String ? pPath.GetString() : null;

                if (profilePath != null)
                {
                    profilePath = $"https://image.tmdb.org/t/p/w200{profilePath}";
                }

                var existingActor = _context.Actors.FirstOrDefault(x => x.Name == name);
                if (existingActor == null)
                {
                    existingActor = new Actor { Name = name, PhotoUrl = profilePath };
                    _context.Actors.Add(existingActor);
                    await _context.SaveChangesAsync();
                }

                _context.MovieActors.Add(new MovieActor
                {
                    MovieId = localMovieId,
                    ActorId = existingActor.Id,
                    Role = character
                });

                count++;
            }
        }

        /// <summary>
        /// Tüm filmler için Gemini text-embedding-004 vektörü oluşturur ve DB'ye kaydeder.
        /// Bu endpoint yalnızca bir kez çalıştırılmalıdır.
        /// GET /api/Seed/GenerateEmbeddings
        /// </summary>
        [HttpGet("GenerateEmbeddings")]
        public async Task<IActionResult> GenerateEmbeddings()
        {
            var geminiKey = _configuration["Gemini:ApiKey"];
            if (string.IsNullOrEmpty(geminiKey))
                return BadRequest("Gemini API Key eksik.");

            var movies = _context.Movies
                .Where(m => m.EmbeddingJson == null)
                .Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieActors).ThenInclude(ma => ma.Actor)
                .ToList();

            if (!movies.Any())
                return Ok(new { message = "Tüm filmlerin zaten embedding'i var." });

            int processed = 0;
            int failed = 0;

            foreach (var movie in movies)
            {
                try
                {
                    var genres = string.Join(", ", movie.MovieGenres.Select(mg => mg.Genre.Name));
                    var actors = string.Join(", ", movie.MovieActors.Take(5).Select(ma => ma.Actor.Name));
                    var text = $"{movie.Title}. {movie.Overview}. Türler: {genres}. Oyuncular: {actors}";

                    var embedding = await GetEmbeddingAsync(geminiKey, text);
                    if (embedding != null)
                    {
                        movie.EmbeddingJson = System.Text.Json.JsonSerializer.Serialize(embedding);
                        await _context.SaveChangesAsync();
                        processed++;
                    }
                    else
                    {
                        failed++;
                    }

                    // API rate limit için bekleme
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Embedding hatası ({movie.Title}): {ex.Message}");
                    failed++;
                    await Task.Delay(1000);
                }
            }

            return Ok(new { message = $"Embedding işlemi tamamlandı. Başarılı: {processed}, Hatalı: {failed}" });
        }

        [HttpPost("RandomizeStock")]
        public async Task<IActionResult> RandomizeStock()
        {
            var movies = await _context.Movies.ToListAsync();
            var random = new Random();
            int updatedCount = 0;

            foreach (var movie in movies)
            {
                movie.TotalCopies = random.Next(1, 11); // Random text between 1 and 10
                updatedCount++;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = $"{updatedCount} filmin stok miktarı 1 ile 10 arasında rastgele sayılarla güncellendi." });
        }

        private async Task<float[]?> GetEmbeddingAsync(string apiKey, string text)
        {
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-001:embedContent?key={apiKey}";
            var body = new
            {
                model = "models/gemini-embedding-001",
                content = new { parts = new[] { new { text } } }
            };

            var content = new System.Net.Http.StringContent(
                System.Text.Json.JsonSerializer.Serialize(body),
                System.Text.Encoding.UTF8,
                "application/json");

            var response = await _httpClient.PostAsync(url, content);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("embedding", out var embEl) &&
                embEl.TryGetProperty("values", out var values))
            {
                return values.EnumerateArray().Select(v => (float)v.GetDouble()).ToArray();
            }

            return null;
        }
    }
}
