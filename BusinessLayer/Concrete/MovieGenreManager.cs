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
    public class MovieGenreManager : IMovieGenreService
    {
        IMovieGenreDal _movieGenreDal;

        public MovieGenreManager(IMovieGenreDal movieGenreDal)
        {
            _movieGenreDal = movieGenreDal;
        }

        public void TAdd(MovieGenre entity)
        {
            _movieGenreDal.Insert(entity);
        }

        public void TDelete(MovieGenre entity)
        {
            _movieGenreDal.Delete(entity);
        }

        public MovieGenre TGetById(int id)
        {
           return _movieGenreDal.GetById(id);
        }

        public List<MovieGenre> TGetList()
        {
           return _movieGenreDal.GetList();
        }

        public void TUpdate(MovieGenre entity)
        {
            _movieGenreDal.Update(entity);
        }
    }
}
