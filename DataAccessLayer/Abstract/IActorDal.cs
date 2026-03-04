using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DataAccessLayer.Abstract
{
    public interface IActorDal: IGenericDal<EntityLayer.Concrete.Actor>
    {
        Actor GetActorWithMovies(int id);  // Aktörün filmlerini de getirecek metod
        List<Actor> GetListWithMovies();   // Tüm aktörleri filmleriyle getirecek metod
    }
}
