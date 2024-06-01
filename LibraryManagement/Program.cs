using Microsoft.EntityFrameworkCore;
using LibraryManagement.Models;
using LibraryManagement.Data;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<LibraryDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("LibraryDB")));
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();

// Function to validate book data
static string? BookDataValidation(string title, DateTime publicationDate, string isbn, string authorName)
{

    if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(isbn) || string.IsNullOrWhiteSpace(authorName) || publicationDate == default)
    {
        return "Invalid book data.";
    }

    if (publicationDate > DateTime.UtcNow)
    {
        return "Publication date cannot be in the future.";
    }

    if (!(isbn.Length == 10 || isbn.Length == 13))
    {
        return "ISBN must be either 10 or 13 characters long.";
    }

    return null;
}

// Endpoint definitions
app.MapGet("/books", async (LibraryDbContext db) =>
{
    var books = await db.Books.Include(b => b.BookAuthors).ThenInclude(ba => ba.Author).ToListAsync();
    var result = books.Select(b => new
    {
        b.Title,
        b.PublicationDate,
        b.ISBN,
        AuthorName = b.BookAuthors.FirstOrDefault()?.Author.Name
    });
    return Results.Ok(result);
});

app.MapGet("/books/{id}", async (int id, LibraryDbContext db) =>
{
    var book = await db.Books.Include(b => b.BookAuthors).ThenInclude(ba => ba.Author).FirstOrDefaultAsync(b => b.Id == id);
    if (book == null) return Results.NotFound("Book not found.");

    var result = new
    {
        book.Title,
        book.PublicationDate,
        book.ISBN,
        AuthorName = book.BookAuthors.FirstOrDefault()?.Author.Name
    };
    return Results.Ok(result);
});

app.MapPost("/books", async (HttpContext context, LibraryDbContext db) =>
{
    using var bodyReader = await JsonDocument.ParseAsync(context.Request.Body);

    if (!bodyReader.RootElement.TryGetProperty("title", out var titleElement) ||
        !bodyReader.RootElement.TryGetProperty("publicationDate", out var publicationDateElement) ||
        !bodyReader.RootElement.TryGetProperty("isbn", out var isbnElement) ||
        !bodyReader.RootElement.TryGetProperty("authorName", out var authorNameElement))
    {
        return Results.BadRequest("Missing required book data.");
    }

    string title = titleElement.GetString()!;
    DateTime publicationDate = DateTime.Parse(publicationDateElement.GetString()!);
    string isbn = isbnElement.GetString()!;
    string authorName = authorNameElement.GetString()!;

    string? errorMessage = BookDataValidation(title, publicationDate, isbn, authorName);

    if (errorMessage != null)
    {
        return Results.BadRequest(errorMessage);
    }

    // Ensure the ISBN is unique
    if (await db.Books.AnyAsync(b => b.ISBN == isbn))
    {
        return Results.BadRequest("ISBN must be unique.");
    }

    // Check if the author exists
    var existingAuthor = await db.Authors.FirstOrDefaultAsync(a => a.Name == authorName);
    if (existingAuthor == null)
    {
        return Results.BadRequest("Author does not exist.");
    }

    var book = new Book
    {
        Title = title,
        PublicationDate = publicationDate,
        ISBN = isbn
    };

    db.Books.Add(book);
    await db.SaveChangesAsync();

    db.BookAuthors.Add(new BookAuthor { BookId = book.Id, AuthorId = existingAuthor.Id });
    await db.SaveChangesAsync();

    var result = new
    {
        book.Title,
        book.PublicationDate,
        book.ISBN,
        AuthorName = existingAuthor.Name
    };
    return Results.Created($"/books/{book.Id}", result);
});

app.MapPut("/books/{id}", async (int id, HttpContext context, LibraryDbContext db) =>
{
    using var bodyReader = await JsonDocument.ParseAsync(context.Request.Body);

    var book = await db.Books.Include(b => b.BookAuthors).ThenInclude(ba => ba.Author).FirstOrDefaultAsync(b => b.Id == id);
    if (book == null) return Results.NotFound("Book not found.");

    if (!bodyReader.RootElement.TryGetProperty("title", out var titleElement) ||
        !bodyReader.RootElement.TryGetProperty("publicationDate", out var publicationDateElement) ||
        !bodyReader.RootElement.TryGetProperty("isbn", out var isbnElement) ||
        !bodyReader.RootElement.TryGetProperty("authorName", out var authorNameElement))
    {
        return Results.BadRequest("Missing required book data.");
    }

    string title = titleElement.GetString()!;
    DateTime publicationDate = DateTime.Parse(publicationDateElement.GetString()!);
    string isbn = isbnElement.GetString()!;
    string authorName = authorNameElement.GetString()!;

    string? errorMessage = BookDataValidation(title, publicationDate, isbn, authorName);

    if (errorMessage != null)
    {
        return Results.BadRequest(errorMessage);
    }

    // Ensure the ISBN is unique or belongs to the same book being updated
    if (await db.Books.AnyAsync(b => b.ISBN == isbn && b.Id != id))
    {
        return Results.BadRequest("ISBN must be unique.");
    }

    // Check if the author name is being modified
    if (book.BookAuthors.FirstOrDefault()?.Author.Name != authorName)
    {
        return Results.BadRequest("Author name cannot be modified.");
    }

    book.Title = title;
    book.PublicationDate = publicationDate;
    book.ISBN = isbn;

    await db.SaveChangesAsync();
    return Results.NoContent();
});


app.MapDelete("/books/{id}", async (int id, LibraryDbContext db) =>
{
    var book = await db.Books.Include(b => b.BookAuthors).FirstOrDefaultAsync(b => b.Id == id);
    if (book == null) return Results.NotFound("Book not found.");

    db.Books.Remove(book);
    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapGet("/authors", async (LibraryDbContext db) =>
{
    var authors = await db.Authors.Include(a => a.BookAuthors).ThenInclude(ba => ba.Book).ToListAsync();
    var result = authors.Select(a => new
    {
        a.Name,
        Books = a.BookAuthors.Select(ba => ba.Book.Title).ToList()
    });
    return Results.Ok(result);
});


app.MapGet("/authors/{id}", async (int id, LibraryDbContext db) =>
{
    var author = await db.Authors.Include(a => a.BookAuthors).ThenInclude(ba => ba.Book).FirstOrDefaultAsync(a => a.Id == id);
    if (author == null) return Results.NotFound("Author not found.");

    var result = new
    {
        author.Name,
        Books = author.BookAuthors.Select(ba => ba.Book.Title).ToList()
    };
    return Results.Ok(result);
});


app.MapPost("/authors", async (HttpContext context, LibraryDbContext db) =>
{
    using var bodyReader = await JsonDocument.ParseAsync(context.Request.Body);

    if (!bodyReader.RootElement.TryGetProperty("name", out var nameElement))
    {
        return Results.BadRequest("Missing author name.");
    }

    string name = nameElement.GetString();

    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest("Author name cannot be null or empty.");
    }

    // Check if the author name already exists
    if (await db.Authors.AnyAsync(a => a.Name == name))
    {
        return Results.BadRequest("Author name already exists.");
    }

    var author = new Author { Name = name };
    db.Authors.Add(author);
    await db.SaveChangesAsync();

    return Results.Created($"/authors/{author.Id}", new { author.Name });
});


app.MapPut("/authors/{id}", async (int id, HttpContext context, LibraryDbContext db) =>
{
    using var bodyReader = await JsonDocument.ParseAsync(context.Request.Body);

    var author = await db.Authors.FirstOrDefaultAsync(a => a.Id == id);
    if (author == null) return Results.NotFound("Author not found.");

    if (!bodyReader.RootElement.TryGetProperty("name", out var nameElement))
    {
        return Results.BadRequest("Missing author name.");
    }

    string name = nameElement.GetString();

    if (string.IsNullOrWhiteSpace(name))
    {
        return Results.BadRequest("Author name cannot be null or empty.");
    }

    // Check if another author with the same name already exists
    if (await db.Authors.AnyAsync(a => a.Name == name && a.Id != id))
    {
        return Results.BadRequest("An author with the same name already exists.");
    }

    author.Name = name;

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.MapDelete("/authors/{id}", async (int id, LibraryDbContext db) =>
{
    var author = await db.Authors.Include(a => a.BookAuthors).ThenInclude(ba => ba.Book).FirstOrDefaultAsync(a => a.Id == id);
    if (author == null) return Results.NotFound("Author not found.");

    // Collect all books associated with the author
    var booksToDelete = author.BookAuthors.Select(ba => ba.Book).ToList();

    // Remove associated book-author relationships
    db.BookAuthors.RemoveRange(author.BookAuthors);

    // Remove the books
    db.Books.RemoveRange(booksToDelete);

    // Remove the author
    db.Authors.Remove(author);

    await db.SaveChangesAsync();
    return Results.NoContent();
});

app.Run();
