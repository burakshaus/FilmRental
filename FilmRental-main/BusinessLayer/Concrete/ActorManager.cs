using BusinessLayer.Abstract;
using DataAccessLayer.Abstract;
using DataAccessLayer.EntityFramework;
using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Concrete
{
    public class ActorManager : IActorService
    {
        IActorDal _actorDal;

        public ActorManager(IActorDal actorDal)
        {
            _actorDal = actorDal;
        }

        public void TAdd(Actor entity)
        {
            _actorDal.Insert(entity);
        }

        public void TDelete(Actor entity)
        {
            _actorDal.Delete(entity);
        }

        public Actor TGetById(int id)
        {
            return _actorDal.GetById(id);
        }

        public List<Actor> TGetList()
        {
          return  _actorDal.GetList();
        }

        public void TUpdate(Actor entity)
        {
            _actorDal.Update(entity);
        }
        public Actor TGetActorWithMovies(int id)
        {
            return _actorDal.GetActorWithMovies(id);
        }

        public List<Actor> TGetListWithMovies()
        {
            return _actorDal.GetListWithMovies();
        }
    }
}
