using System.ComponentModel.DataAnnotations;

namespace LibraryManagement.Models
{
    /// <summary>
    /// Represents an author in the library management system.
    /// </summary>
    public class Author
    {
        public int Id { get; set; }

        [Required]
        public string? Name { get; set; }
        public IEnumerable<BookAuthor>? BookAuthors { get; set; }
    }

}
