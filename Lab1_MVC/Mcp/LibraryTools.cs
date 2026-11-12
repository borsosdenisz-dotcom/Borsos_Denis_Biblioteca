// Mcp/LibraryTools.cs
using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Lab1_MVC.Data;
using Lab1_MVC.Services;
// există și Lab1_MVC.Models.Recommendation; recommend_books folosește record-ul din RecommendationService (ca la Î6)
using Recommendation = Lab1_MVC.Services.Recommendation;

namespace Lab1_MVC.Mcp;

public record BookInfo(int Id, string Title, string? Author, string? Genre, int Year, decimal Price);

public record BookDetails(int Id, string Title, string? Author, string? Genre, int Year,
                          decimal Price, string? Description, int Orders);

[McpServerToolType]
public class LibraryTools
{
    [McpServerTool(Name = "books_by_year", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Întoarce maximum 10 cărți publicate într-un interval inclusiv de ani, ordonate după an și titlu.")]
    public static async Task<List<BookInfo>> BooksByYear(
        LibraryContext db,
        [Description("Primul an al intervalului, inclusiv; trebuie să fie între 1000 și anul curent.")] int startYear,
        [Description("Ultimul an al intervalului, inclusiv; trebuie să fie între anul de început și anul curent.")] int endYear,
        CancellationToken ct)
    {
        var currentYear = DateTime.UtcNow.Year;
        if (startYear is < 1000 || startYear > currentYear)
            throw new McpException($"Anul de început trebuie să fie între 1000 și {currentYear}.");
        if (endYear is < 1000 || endYear > currentYear)
            throw new McpException($"Anul de sfârșit trebuie să fie între 1000 și {currentYear}.");
        if (startYear > endYear)
            throw new McpException("Interval invalid: anul de început trebuie să fie mai mic sau egal cu anul de sfârșit.");

        return await db.Book.AsNoTracking()
            .Where(book => book.Year >= startYear && book.Year <= endYear)
            .OrderBy(book => book.Year)
            .ThenBy(book => book.Title)
            .Take(10)
            .Select(book => new BookInfo(
                book.ID,
                book.Title,
                book.Author == null ? null : book.Author.FirstName + " " + book.Author.LastName,
                book.Genre == null ? null : book.Genre.Name,
                book.Year,
                book.Price))
            .ToListAsync(ct);
    }

    [McpServerTool(Name = "search_books", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Cauta carti in catalogul Bibliotecii dupa un cuvant din titlu sau din numele autorului. " +
                 "Intoarce cel mult 10 carti, cu ID, titlu, autor, gen, an si pret.")]
    public static async Task<List<BookInfo>> SearchBooks(
        LibraryContext db,                                    
        [Description("Cuvantul cautat, de exemplu 'Rebreanu' sau 'Ion'.")] string query,
        CancellationToken ct)
    {
        var q = query.Trim();
        return await db.Book.AsNoTracking()
            .Where(b => b.Title.Contains(q)
                     || (b.Author != null && (b.Author.LastName.Contains(q) || b.Author.FirstName.Contains(q))))
            .OrderBy(b => b.Title)
            .Take(10)
            .Select(b => new BookInfo(b.ID, b.Title,
                b.Author == null ? null : b.Author.FirstName + " " + b.Author.LastName,
                b.Genre == null ? null : b.Genre.Name,
                b.Year, b.Price))
            .ToListAsync(ct);
    }

    [McpServerTool(Name = "get_book_details", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Intoarce detaliile unei carti dupa ID: autor, gen, an, pret, descriere si numarul de comenzi. " +
                 "Daca nu stii ID-ul, cauta intai cartea cu search_books.")]
    public static async Task<BookDetails?> GetBookDetails(
        LibraryContext db,
        [Description("ID-ul cartii din catalog.")] int bookId,
        CancellationToken ct)
    {
        return await db.Book.AsNoTracking()
            .Where(b => b.ID == bookId)
            .Select(b => new BookDetails(b.ID, b.Title,
                b.Author == null ? null : b.Author.FirstName + " " + b.Author.LastName,
                b.Genre == null ? null : b.Genre.Name,
                b.Year, b.Price, b.Description, b.Orders.Count()))
            .FirstOrDefaultAsync(ct);
    }

    [McpServerTool(Name = "recommend_books", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Recomandă carti unui cititor, dupa evaluarile lui (modelul ML.NET al Bibliotecii). " +
                 "Pentru un cititor fara evaluari intoarce cele mai bine evaluate carti (Fallback = true).")]
    public static async Task<List<Recommendation>> RecommendBooks(
        RecommendationService recs,                           // serviciul din I6
        [Description("ID-ul cititorului (ReaderId).")] int readerId,
        [Description("Câte recomandări (1–10). Implicit 5.")] int top = 5,
        CancellationToken ct = default)
    {
        return await recs.RecommendAsync(readerId, Math.Clamp(top, 1, 10), ct);
    }

    // ---------- Misiunea 3 · tool-ul nou  ----------
    [McpServerTool(Name = "top_books_by_price", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Intoarce cele mai scumpe sau cele mai ieftine carti din catalog.")]
    public static async Task<List<BookInfo>> TopBooksByPrice(
        LibraryContext db,
        [Description("true = cele mai scumpe; false = cele mai ieftine.")] bool mostExpensive,
        [Description("Câte cărți (1–10). Implicit 3.")] int top = 3,
        CancellationToken ct = default)
    {
        var books = db.Book.AsNoTracking();
        books = mostExpensive ? books.OrderByDescending(b => b.Price) : books.OrderBy(b => b.Price);
        return await books.Take(Math.Clamp(top, 1, 10))
            .Select(b => new BookInfo(b.ID, b.Title,
                b.Author == null ? null : b.Author.FirstName + " " + b.Author.LastName,
                b.Genre == null ? null : b.Genre.Name, b.Year, b.Price))
            .ToListAsync(ct);
    }

    // ---------- Misiunea 3 · books_by_genre (scris de echipă) ----------
    [McpServerTool(Name = "books_by_genre", ReadOnly = true, Destructive = false, OpenWorld = false)]
    [Description("Intoarce cartile dintr-un gen al catalogului (de exemplu SF, Fantasy, Polițist, Roman românesc), " +
                 "ordonate dupa titlu, cu ID, titlu, autor, gen, an si pret.")]
    public static async Task<List<BookInfo>> BooksByGenre(
        LibraryContext db,
        [Description("Numele genului, de exemplu 'SF', 'Fantasy' sau 'Roman românesc'.")] string genre,
        CancellationToken ct)
    {
        var g = genre.Trim();
        return await db.Book.AsNoTracking()
            .Where(b => b.Genre != null && b.Genre.Name == g)
            .OrderBy(b => b.Title)
            .Select(b => new BookInfo(b.ID, b.Title,
                b.Author == null ? null : b.Author.FirstName + " " + b.Author.LastName,
                b.Genre == null ? null : b.Genre.Name, b.Year, b.Price))
            .ToListAsync(ct);
    }
}
