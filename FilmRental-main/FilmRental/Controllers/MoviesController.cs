using BusinessLayer.Concrete;
using DataAccessLayer.EntityFramework;
using Microsoft.AspNetCore.Mvc;

namespace FilmRental.Controllers
{
    public class MoviesController : Controller
    {
        private readonly MovieManager _movieManager;

        public MoviesController()
        {
            _movieManager = new MovieManager(new EfMovieDal());
        }

        public IActionResult Index(int? genreId, double? minRating, int? year)
        {
            using var context = new DataAccessLayer.Concrete.Context();
            
            // Tüm Türleri View'a Yolla (Dropdown İçin)
            ViewBag.Genres = context.Genres.OrderBy(g => g.Name).ToList();

            // Süzgeçleri (Filtreleri) View'da hatırla
            ViewBag.SelectedGenreId = genreId;
            ViewBag.SelectedMinRating = minRating;
            ViewBag.SelectedYear = year;

            var query = context.Movies.AsQueryable();

            if (genreId.HasValue)
            {
                query = query.Where(m => m.MovieGenres.Any(mg => mg.GenreId == genreId.Value));
            }

            if (minRating.HasValue)
            {
                query = query.Where(m => m.ImdbRating >= minRating.Value);
            }

            if (year.HasValue)
            {
                query = query.Where(m => m.ReleaseDate.HasValue && m.ReleaseDate.Value.Year >= year.Value);
            }

            var results = query.OrderByDescending(m => m.ImdbRating).ToList();
            return View(results);
        }

        public IActionResult Details(int id)
        {
            var movie = _movieManager.TGetMovieWithDetails(id);
            if (movie == null)
            {
                return NotFound();
            }
            return View(movie);
        }

        public IActionResult Search(string q)
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return RedirectToAction("Index");
            }

            using var context = new DataAccessLayer.Concrete.Context();
            var results = context.Movies
                .Where(m => m.Title.Contains(q))
                .OrderByDescending(m => m.ImdbRating)
                .ToList();

            ViewBag.SearchQuery = q;
            return View(results);
        }
    }
}
