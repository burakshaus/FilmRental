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
    public class EfActorDal: GenericRepository<Actor>,IActorDal
    {
        public Actor GetActorWithMovies(int id)
        {
            using var c = new Context();
            return c.Actors
                .Include(x => x.MovieActors)
                    .ThenInclude(ma => ma.Movie)
                .FirstOrDefault(x => x.Id == id);
        }

        public List<Actor> GetListWithMovies()
        {
            using var c = new Context();
            return c.Actors
                .Include(x => x.MovieActors)
                    .ThenInclude(ma => ma.Movie)
                .ToList();
        }
    }
}
