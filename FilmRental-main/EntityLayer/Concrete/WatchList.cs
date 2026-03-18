using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityLayer.Concrete
{
    public class WatchList
    {
        [Key]
        public int Id { get; set; }  // WatchListId yerine Id kullanıyoruz
        public int UserId { get; set; }  // string yerine int kullanıyoruz
        public int MovieId { get; set; }
        public bool IsWatched { get; set; }
        public bool IsFavorite { get; set; }
        public DateTime AddedDate { get; set; }

        public Movie Movie { get; set; }
    }
}
