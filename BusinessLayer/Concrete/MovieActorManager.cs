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
    public class MovieActorManager : IMovieActorService
    {
        IMovieActorDal _movieActorDal;

        public MovieActorManager(IMovieActorDal movieActorDal)
        {
            _movieActorDal = movieActorDal;
        }

        public void TAdd(MovieActor entity)
        {
           _movieActorDal.Insert(entity);
        }

        public void TDelete(MovieActor entity)
        {
         _movieActorDal.Delete(entity);
        }

        public MovieActor TGetById(int id)
        {
            return _movieActorDal.GetById(id);
        }

        public List<MovieActor> TGetList()
        {
            return new List<MovieActor>();
        }

        public void TUpdate(MovieActor entity)
        {
            _movieActorDal.Update(entity);
        }
    }
}
