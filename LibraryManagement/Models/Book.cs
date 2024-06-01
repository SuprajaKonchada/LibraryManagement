using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Models
{
    /// <summary>
    /// Represents a book in the library management system.
    /// </summary>
    public class Book
    {
        public int Id { get; set; }

        [Required]
        public string? Title { get; set; }

        [Required]
        public DateTime PublicationDate { get; set; }

        [Required]
        public string? ISBN { get; set; }

        public IEnumerable<BookAuthor>? BookAuthors { get; set; }
    }
}
