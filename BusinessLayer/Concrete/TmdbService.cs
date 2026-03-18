using BusinessLayer.Abstract;
using DataAccessLayer.Concrete;
using EntityLayer.Concrete;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace BusinessLayer.Concrete
{
    public class TmdbService : ITmdbService
    {
        private readonly Context _context;
        private readonly HttpClient _httpClient;

        public TmdbService(Context context)
        {
            _context = context;
            _httpClient = new HttpClient();
        }

        public async Task<bool> SearchAndAddMovieAsync(string query, string apiKey)
        {
            var searchUrl = $"https://api.themoviedb.org/3/search/movie?api_key={apiKey}&language=tr-TR&query={Uri.EscapeDataString(query)}&page=1";
            Console.WriteLine($"[DEBUG] TMDB Search URL: {searchUrl}");
            
            var response = await _httpClient.GetAsync(searchUrl);
            
            if (!response.IsSuccessStatusCode)
            {
                var errBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[ERROR] TMDB API HTTP {response.StatusCode}: {errBody}");
                return false;
            }

            var jsonString = await response.Content.ReadAsStringAsync();
            using var jsonDoc = JsonDocument.Parse(jsonString);
            var results = jsonDoc.RootElement.GetProperty("results");

            Console.WriteLine($"[DEBUG] TMDB Search Results Count: {results.GetArrayLength()}");

            if (results.GetArrayLength() == 0) return false;

            var m = results[0]; 
            var tmdbId = m.GetProperty("id").GetInt32();
            var title = (m.TryGetProperty("title", out var tEl) ? tEl.GetString() : null) ?? query;
            
            Console.WriteLine($"[DEBUG] Best match from TMDB: {title} (ID: {tmdbId})");
            
            // Eğer film adıyla veritabanında zaten varsa çekmene gerek yok
            if (_context.Movies.Any(x => x.Title.ToLower() == title.ToLower())) 
            {
                Console.WriteLine($"[DEBUG] Movie '{title}' already exists in local DB. Skipping TMDB fetch.");
                return true;
            }

            var overview = m.GetProperty("overview").GetString() ?? "Bilgi Yok";
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
                ReleaseDate = releaseDate,
                TotalCopies = 0 // Kullanıcının istediği gibi 0 stokla ekle
            };

            _context.Movies.Add(newMovie);
            await _context.SaveChangesAsync();

            // Genre Çek ve Eşleştir
            var genreMap = await FetchAndMapGenresAsync(apiKey);
            if (m.TryGetProperty("genre_ids", out var genreIds))
            {
                foreach (var gId in genreIds.EnumerateArray())
                {
                    int tmdbGenreId = gId.GetInt32();
                    if (genreMap.TryGetValue(tmdbGenreId, out int localGenreId))
                    {
                        var existsBefore = _context.MovieGenres.Any(mg => mg.MovieId == newMovie.Id && mg.GenreId == localGenreId);
                        if (!existsBefore) 
                        {
                            _context.MovieGenres.Add(new MovieGenre
                            {
                                MovieId = newMovie.Id,
                                GenreId = localGenreId
                            });
                        }
                    }
                }
            }

            // Oyuncuları Çek
            await FetchAndSeedCreditsAsync(apiKey, tmdbId, newMovie.Id);
            
            await _context.SaveChangesAsync();
            return true;
        }

        private async Task<Dictionary<int, int>> FetchAndMapGenresAsync(string apiKey)
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
                string name = g.GetProperty("name").GetString() ?? "Bilinmiyor";

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
            if (!jsonDoc.RootElement.TryGetProperty("cast", out var cast)) return;

            int count = 0;
            foreach (var c in cast.EnumerateArray())
            {
                if (count >= 5) break;

                string name = c.GetProperty("name").GetString() ?? "Bilinmiyor";
                var characterProp = c.GetProperty("character");
                string character = characterProp.ValueKind == JsonValueKind.String ? characterProp.GetString()! : "Bilinmiyor";
                string? profilePath = c.TryGetProperty("profile_path", out var pPath) && pPath.ValueKind == JsonValueKind.String ? pPath.GetString() : null;

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

                var existsBefore = _context.MovieActors.Any(ma => ma.MovieId == localMovieId && ma.ActorId == existingActor.Id);
                if (!existsBefore)
                {
                    _context.MovieActors.Add(new MovieActor
                    {
                        MovieId = localMovieId,
                        ActorId = existingActor.Id,
                        Role = character
                    });
                }

                count++;
            }
        }
    }
}
