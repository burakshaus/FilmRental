using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityLayer.Concrete
{
    public class MovieActor
    {
        // Composite key için [Key] attribute'unu kaldırıyoruz
        public int MovieId { get; set; }
        public int ActorId { get; set; }
        public string? Role { get; set; }

        public Movie Movie { get; set; }
        public Actor Actor { get; set; }
    }

}
