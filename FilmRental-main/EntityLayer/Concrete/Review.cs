using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityLayer.Concrete
{
    public class Review
    {
        [Key]
        public int Id { get; set; }  // ReviewId yerine Id kullanıyoruz
        public float Rating { get; set; }  // int yerine float kullanıyoruz çünkü IMDB puanları ondalıklı
        public string? Comment { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int MovieId { get; set; }
        public int UserId { get; set; }  // string yerine int kullanıyoruz

        public Movie Movie { get; set; }
    }
}
