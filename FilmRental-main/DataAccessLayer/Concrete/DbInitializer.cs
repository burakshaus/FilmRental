using EntityLayer.Concrete;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace DataAccessLayer.Concrete
{
    public static class DbInitializer
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new Context(
                serviceProvider.GetRequiredService<DbContextOptions<Context>>()))
            {
                // Create db if needed
                context.Database.EnsureCreated();

                // Look for any movies
                if (context.Movies.Any())
                {
                    return;   // DB has been seeded
                }

                var genres = new Genre[]
                {
                    new Genre { Name = "Bilim Kurgu" },
                    new Genre { Name = "Aksiyon" },
                    new Genre { Name = "Dram" }
                };
                foreach (Genre g in genres)
                {
                    context.Genres.Add(g);
                }
                context.SaveChanges();

                var actors = new Actor[]
                {
                    new Actor { Name = "Keanu Reeves", PhotoUrl = "https://m.media-amazon.com/images/M/MV5BNzQzOTk3OTAtNDQ0Zi00ZTVkLWI0MTEtMDllZjNkYzNjNTc4L2ltYWdlXkEyXkFqcGdeQXVyNjU0OTQ0OTY@._V1_.jpg" },
                    new Actor { Name = "Christian Bale", PhotoUrl = "https://m.media-amazon.com/images/M/MV5BMTMxNTMwODM0NF5BMl5BanBnXkFtZTcwODAyMTk2Mw@@._V1_.jpg" },
                    new Actor { Name = "Matthew McConaughey", PhotoUrl = "https://m.media-amazon.com/images/M/MV5BZjdkOTU3MDktN2IxOS00OGEyLWFmMjktY2FiMmZkNWIyODZiXkEyXkFqcGdeQXVyMTMxODk2OTU@._V1_.jpg" }
                };
                foreach (Actor a in actors)
                {
                    context.Actors.Add(a);
                }
                context.SaveChanges();

                var movies = new Movie[]
                {
                    new Movie { Title = "The Matrix", ReleaseDate = new DateTime(1999, 3, 31), PosterPath = "https://m.media-amazon.com/images/M/MV5BNzQzOTk3OTAtNDQ0Zi00ZTVkLWI0MTEtMDllZjNkYzNjNTc4L2ltYWdlXkEyXkFqcGdeQXVyNjU0OTQ0OTY@._V1_.jpg", Director = "Lana Wachowski, Lilly Wachowski", Overview = "A computer hacker learns from mysterious rebels about the true nature of his reality...", ImdbRating = 8.7, Runtime = 136 },
                    new Movie { Title = "The Dark Knight", ReleaseDate = new DateTime(2008, 7, 18), PosterPath = "https://m.media-amazon.com/images/M/MV5BMTMxNTMwODM0NF5BMl5BanBnXkFtZTcwODAyMTk2Mw@@._V1_.jpg", Director = "Christopher Nolan", Overview = "When the menace known as the Joker wreaks havoc and chaos on the people of Gotham...", ImdbRating = 9.0, Runtime = 152 },
                    new Movie { Title = "Interstellar", ReleaseDate = new DateTime(2014, 11, 7), PosterPath = "https://m.media-amazon.com/images/M/MV5BZjdkOTU3MDktN2IxOS00OGEyLWFmMjktY2FiMmZkNWIyODZiXkEyXkFqcGdeQXVyMTMxODk2OTU@._V1_.jpg", Director = "Christopher Nolan", Overview = "A team of explorers travel through a wormhole in space in an attempt to ensure humanity's survival.", ImdbRating = 8.7, Runtime = 169 }
                };
                foreach (Movie m in movies)
                {
                    context.Movies.Add(m);
                }
                context.SaveChanges();
                
                var movieActors = new MovieActor[]
                {
                    new MovieActor { MovieId = movies[0].Id, ActorId = actors[0].Id },
                    new MovieActor { MovieId = movies[1].Id, ActorId = actors[1].Id },
                    new MovieActor { MovieId = movies[2].Id, ActorId = actors[2].Id }
                };
                foreach (MovieActor ma in movieActors)
                {
                    context.MovieActors.Add(ma);
                }
                context.SaveChanges();

                var movieGenres = new MovieGenre[]
                {
                    new MovieGenre { MovieId = movies[0].Id, GenreId = genres[0].Id },
                    new MovieGenre { MovieId = movies[0].Id, GenreId = genres[1].Id },
                    new MovieGenre { MovieId = movies[1].Id, GenreId = genres[1].Id },
                    new MovieGenre { MovieId = movies[1].Id, GenreId = genres[2].Id },
                    new MovieGenre { MovieId = movies[2].Id, GenreId = genres[0].Id },
                    new MovieGenre { MovieId = movies[2].Id, GenreId = genres[2].Id }
                };
                foreach (MovieGenre mg in movieGenres)
                {
                    context.MovieGenres.Add(mg);
                }
                context.SaveChanges();
            }
        }
    }
}
