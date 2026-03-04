using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityLayer.Concrete
{
    public class Actor
    {
        [Key]
        public int Id { get; set; }  // ActorId yerine Id kullanıyoruz
        public string Name { get; set; }
        public string? PhotoUrl { get; set; }
        public ICollection<MovieActor> MovieActors { get; set; }
    }
}
