using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EntityLayer.Concrete
{
    public class Movie
    {
        public Movie()
        {
            MovieGenres = new HashSet<MovieGenre>();
            MovieActors = new HashSet<MovieActor>();
            Reviews = new HashSet<Review>();
            WatchLists = new HashSet<WatchList>();
        }
        [Key]
        public int Id { get; set; }
        [Required]
        public string Title { get; set; }
        public string? Overview { get; set; }
        public string? PosterPath { get; set; }
        public double? ImdbRating { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string? Director { get; set; }
        public int? Runtime { get; set; }
        public string? TrailerUrl { get; set; }
        public string? EmbeddingJson { get; set; } // Gemini text-embedding-004 vektörü (768 boyut, JSON)
        public int TotalCopies { get; set; } = 3; // DVD kopya sayısı (varsayılan 3)

        // İlişkiler
        public ICollection<MovieGenre> MovieGenres { get; set; }
        public ICollection<Review> Reviews { get; set; }
        public ICollection<WatchList> WatchLists { get; set; }
        public ICollection<MovieActor> MovieActors { get; set; }
    }

}
