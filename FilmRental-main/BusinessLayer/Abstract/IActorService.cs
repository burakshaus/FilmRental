using EntityLayer.Concrete;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BusinessLayer.Abstract
{
    internal interface IActorService:IGenericService<Actor>
    {
        Actor TGetActorWithMovies(int id);
        List<Actor> TGetListWithMovies();
    }
}
