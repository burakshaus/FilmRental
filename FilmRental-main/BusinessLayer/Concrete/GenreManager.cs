using BusinessLayer.Abstract;
using DataAccessLayer.Abstract;
using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Concrete
{
    public class GenreManager : IGenreService
    {
        IGenreDal _genreDal;

        public GenreManager(IGenreDal genreDal)
        {
            _genreDal = genreDal;
        }

        public void TAdd(Genre entity)
        {
           _genreDal.Insert(entity);
        }

        public void TDelete(Genre entity)
        {
           _genreDal.Delete(entity);
        }

        public Genre TGetById(int id)
        {
            return _genreDal.GetById(id);
        }

        public List<Genre> TGetList()
        {
            return _genreDal.GetList();
        }

        public void TUpdate(Genre entity)
        {
            _genreDal.Update(entity);
        }
        public List<Genre> TGetListWithMovieCount()
        {
            return _genreDal.GetListWithMovieCount();
        }
    }
}
