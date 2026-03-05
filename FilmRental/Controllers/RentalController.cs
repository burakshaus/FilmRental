using DataAccessLayer.Concrete;
using EntityLayer.Concrete;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FilmRental.Controllers
{
    public class RentalController : Controller
    {
        private readonly Context _context;

        public RentalController(Context context)
        {
            _context = context;
        }

        // GET /Rental — Admin dashboard
        public IActionResult Index()
        {
            var movies = _context.Movies
                .Select(m => new
                {
                    Movie = m,
                    ActiveRentals = _context.Rentals.Count(r => r.MovieId == m.Id && r.ReturnedAt == null),
                    TotalCopies = m.TotalCopies
                })
                .OrderBy(x => x.Movie.Title)
                .AsEnumerable()
                .Select(x => new MovieStockViewModel
                {
                    Id = x.Movie.Id,
                    Title = x.Movie.Title,
                    TotalCopies = x.TotalCopies,
                    RentedCopies = x.ActiveRentals,
                    AvailableCopies = x.TotalCopies - x.ActiveRentals
                })
                .ToList();

            var activeRentals = _context.Rentals
                .Include(r => r.Movie)
                .Where(r => r.ReturnedAt == null)
                .OrderByDescending(r => r.RentedAt)
                .ToList();

            ViewBag.ActiveRentals = activeRentals;
            return View(movies);
        }

        // POST /Rental/Rent
        [HttpPost]
        public IActionResult Rent(int movieId, string customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName))
            {
                TempData["Error"] = "Müşteri adı boş olamaz.";
                return RedirectToAction("Index");
            }

            var movie = _context.Movies.Find(movieId);
            if (movie == null)
            {
                TempData["Error"] = "Film bulunamadı.";
                return RedirectToAction("Index");
            }

            var activeRentals = _context.Rentals.Count(r => r.MovieId == movieId && r.ReturnedAt == null);
            if (activeRentals >= movie.TotalCopies)
            {
                TempData["Error"] = $"Üzgünüz, \"{movie.Title}\" filminin tüm kopyaları kirada!";
                return RedirectToAction("Index");
            }

            _context.Rentals.Add(new Rental
            {
                MovieId = movieId,
                CustomerName = customerName.Trim(),
                RentedAt = DateTime.Now
            });
            _context.SaveChanges();

            TempData["Success"] = $"\"{movie.Title}\" filmi {customerName} adına kiralandı! ✅";
            return RedirectToAction("Index");
        }

        // POST /Rental/Return/{rentalId}
        [HttpPost]
        public IActionResult Return(int rentalId)
        {
            var rental = _context.Rentals.Include(r => r.Movie).FirstOrDefault(r => r.Id == rentalId);
            if (rental == null)
            {
                TempData["Error"] = "Kiralama kaydı bulunamadı.";
                return RedirectToAction("Index");
            }

            rental.ReturnedAt = DateTime.Now;
            _context.SaveChanges();

            TempData["Success"] = $"\"{rental.Movie.Title}\" iade alındı. Teşekkürler! ✅";
            return RedirectToAction("Index");
        }
    }

    public class MovieStockViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public int TotalCopies { get; set; }
        public int RentedCopies { get; set; }
        public int AvailableCopies { get; set; }
    }
}
