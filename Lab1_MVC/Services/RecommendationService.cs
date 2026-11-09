// Services/RecommendationService.cs
// Logica de recomandare, scoasq din endpoint-ul Minimal API din Laboratorul I5 (A3),
// ca sa poată fi folosita de ACELASI cod si prin REST, si prin gRPC.
using Lab1_MVC.Data;
using Lab1_MVC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.ML;

namespace Lab1_MVC.Services;

public record Recommendation(int BookId, string Title, float Score, bool Fallback);

public class RecommendationService(
    LibraryContext db,
    PredictionEnginePool<BookRating, BookRatingPrediction> pool)
{
    // Pragul de cold start
    public const int MinRatings = 8;

    public async Task<List<Recommendation>> RecommendAsync(
        int readerId, int top, CancellationToken ct = default)
    {
        var n = Math.Clamp(top <= 0 ? 5 : top, 1, 20);

        var rated = await db.Rating
            .Where(r => r.ReaderId == readerId)
            .Select(r => r.BookID)
            .ToListAsync(ct);

        var candidates = await db.Book.AsNoTracking()
            .Where(b => !rated.Contains(b.ID))
            .Select(b => new { b.ID, b.Title })
            .ToListAsync(ct);

        var recs = candidates
            .Select(b => new Recommendation(b.ID, b.Title,
                pool.Predict("BookRecommender",
                    new BookRating { UserId = readerId, BookId = b.ID }).Score,
                false))
            .Where(r => !float.IsNaN(r.Score))
            .OrderByDescending(r => r.Score)
            .Take(n)
            .ToList();

        if (recs.Count > 0) return recs;

        // cold start: cele mai bine evaluate cărți, cu cel puțin MinRatings evaluări
        var popular = await db.Rating
            .GroupBy(r => r.BookID)
            .Where(g => g.Count() >= MinRatings)
            .Select(g => new { BookID = g.Key, Avg = g.Average(r => r.Stars) })
            .OrderByDescending(x => x.Avg)
            .Take(n)
            .ToListAsync(ct);

        var ids = popular.Select(p => p.BookID).ToList();
        var titles = await db.Book
            .Where(b => ids.Contains(b.ID))
            .ToDictionaryAsync(b => b.ID, b => b.Title, ct);

        return popular
            .Select(p => new Recommendation(p.BookID, titles[p.BookID], (float)p.Avg, true))
            .ToList();
    }
}
