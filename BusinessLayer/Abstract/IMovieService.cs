using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Abstract
{
    internal interface IMovieService : IGenericService<Movie>
    {
        Movie TGetMovieWithDetails(int id);
        List<Movie> TGetListByGenre(int genreId);
    }
}
