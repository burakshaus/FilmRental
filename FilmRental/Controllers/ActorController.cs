using BusinessLayer.Concrete;
using DataAccessLayer.EntityFramework;
using Microsoft.AspNetCore.Mvc;

namespace FilmRental.Controllers
{
    public class ActorController : Controller
    {
        private readonly ActorManager _actorManager;

        public ActorController()
        {
            _actorManager = new ActorManager(new EfActorDal());
        }

        public IActionResult Index()
        {
            var values = _actorManager.TGetListWithMovies();  // Filmleriyle birlikte getir
            return View(values);
        }

        public IActionResult Details(int id)
        {
            var actor = _actorManager.TGetActorWithMovies(id);
            if (actor == null)
            {
                return NotFound();
            }
            return View(actor);
        }
    }
}
