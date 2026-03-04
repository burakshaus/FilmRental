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
    public class MovieManager : IMovieService
    {
        IMovieDal _movieDal;

        public MovieManager(IMovieDal movieDal)
        {
            _movieDal = movieDal;
        }

        public void TAdd(Movie entity)
        {
            _movieDal.Insert(entity);
        }

        public void TDelete(Movie entity)
        {
            _movieDal.Delete(entity);
        }

        public Movie TGetById(int id)
        {
            return _movieDal.GetById(id);
        }

        public List<Movie> TGetList()
        {
          return _movieDal.GetList();
        }

        public void TUpdate(Movie entity)
        {
           _movieDal.Update(entity);
        }
        public List<Movie> TGetListByGenre(int genreId)  // Yeni metod
        {
            return _movieDal.GetListByGenre(genreId);
        }
        public Movie TGetMovieWithDetails(int id)
        {
            return _movieDal.GetMovieWithDetails(id);
        }
    }

}
