using DataAccessLayer.Abstract;
using DataAccessLayer.Concrete;
using DataAccessLayer.Repository;
using EntityLayer.Concrete;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.EntityFramework
{
    public class EfMovieDal : GenericRepository<Movie>, IMovieDal
    {
        public Movie GetMovieWithDetails(int id)
        {
            using var c = new Context();
            return c.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieActors)
                    .ThenInclude(ma => ma.Actor)
                .Include(m => m.Reviews)
                .FirstOrDefault(m => m.Id == id);
        }
        public List<Movie> GetListByGenre(int genreId)
        {
            using var c = new Context();
            return c.Movies
                .Include(m => m.MovieGenres)
                    .ThenInclude(mg => mg.Genre)
                .Include(m => m.MovieActors)
                    .ThenInclude(ma => ma.Actor)
                .Where(m => m.MovieGenres.Any(mg => mg.GenreId == genreId))
                .ToList();
        }

    }
}
