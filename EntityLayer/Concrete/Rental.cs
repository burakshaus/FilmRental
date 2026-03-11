using System;
using System.ComponentModel.DataAnnotations;

namespace EntityLayer.Concrete
{
    public class Rental
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int MovieId { get; set; }
        public Movie Movie { get; set; } = null!;

        [Required]
        public string CustomerName { get; set; } = "";

        public DateTime RentedAt { get; set; } = DateTime.Now;

        /// <summary>
        /// Null means the DVD has not been returned yet (currently rented).
        /// </summary>
        public DateTime? ReturnedAt { get; set; }

        public bool IsActive => ReturnedAt == null;
    }
}
