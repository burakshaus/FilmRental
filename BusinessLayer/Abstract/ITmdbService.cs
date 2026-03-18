using System.Threading.Tasks;

namespace BusinessLayer.Abstract
{
    public interface ITmdbService
    {
        Task<bool> SearchAndAddMovieAsync(string query, string apiKey);
    }
}
