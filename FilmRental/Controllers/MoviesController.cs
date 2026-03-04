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

        public IActionResult Index()
        {
            var values = _movieManager.TGetList();
            return View(values);
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
    }
}
