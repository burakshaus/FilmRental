using BusinessLayer.Concrete;
using DataAccessLayer.EntityFramework;
using Microsoft.AspNetCore.Mvc;

namespace FilmRental.Controllers
{
    public class GenreController : Controller
    {
        private readonly GenreManager _genreManager;
        private readonly MovieManager _movieManager;

        public GenreController()
        {
            _genreManager = new GenreManager(new EfGenreDal());
            _movieManager = new MovieManager(new EfMovieDal());
        }

        public IActionResult Index()
        {
            var values = _genreManager.TGetListWithMovieCount();  // Değişen kısım
            return View(values);
        }

        public IActionResult Movies(int id)
        {
            var values = _movieManager.TGetListByGenre(id);
            var genre = _genreManager.TGetById(id);
            ViewBag.GenreName = genre?.Name;
            return View(values);
        }
    }
}