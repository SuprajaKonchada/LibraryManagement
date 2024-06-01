namespace LibraryManagement.Models
{
    /// <summary>
    /// Represents the association between a book and its author in the library management system.
    /// </summary>
    public class BookAuthor
    {
        public int BookId { get; set; }
        public Book? Book { get; set; }

        public int AuthorId { get; set; }
        public Author? Author { get; set; }
    }
}
