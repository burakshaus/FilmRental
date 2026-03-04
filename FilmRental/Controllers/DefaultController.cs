using Microsoft.AspNetCore.Mvc;

namespace FilmRental.Controllers
{
    public class DefaultController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
