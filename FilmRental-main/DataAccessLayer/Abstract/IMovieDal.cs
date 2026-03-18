using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Abstract
{
    public interface IMovieDal : IGenericDal<EntityLayer.Concrete.Movie>
    {
        List<Movie> GetListByGenre(int genreId);
        Movie GetMovieWithDetails(int id);
    }
}
