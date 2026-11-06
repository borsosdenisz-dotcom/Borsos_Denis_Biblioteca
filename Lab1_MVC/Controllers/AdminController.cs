using Lab1_MVC.Data;
using Lab1_MVC.Models;
using Lab1_MVC.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lab1_MVC.Controllers;

public class AdminController(LibraryContext db, RecommenderTrainer trainer) : Controller
{
    public async Task<IActionResult> Index()
    {
        ViewBag.Ratings = await db.Rating.CountAsync();
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportRatings(IFormFile? file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Msg"] = "Alegeți un fișier CSV.";
            return RedirectToAction(nameof(Index));
        }

        var ratings = new List<Rating>();
        using var reader = new StreamReader(file.OpenReadStream());
        await reader.ReadLineAsync();                       // sărim antetul
        string? line;
        while ((line = await reader.ReadLineAsync()) != null)
        {
            var p = line.Split(',');
            if (p.Length < 3 || !int.TryParse(p[0], out var u) ||
                !int.TryParse(p[1], out var b) || !int.TryParse(p[2], out var s))
                continue;
            ratings.Add(new Rating { ReaderId = u, BookID = b, Stars = s, CreatedAt = DateTime.UtcNow });
        }

        db.Rating.AddRange(ratings);
        await db.SaveChangesAsync();
        TempData["Msg"] = $"Au fost importate {ratings.Count} evaluări.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Retrain()
    {
        var (rmse, count) = await trainer.TrainAsync();
        TempData["Msg"] = $"Model reantrenat pe {count} evaluări. RMSE = {rmse:0.00}";
        return RedirectToAction(nameof(Index));
    }

    // S1: jurnalul recomandărilor - filtru după cititor și interval de date, sortare după dată, paginare.
    // Formularul de filtrare nu trimite pageNumber, deci orice schimbare a filtrului pornește de la pagina 1.
    public async Task<IActionResult> Log(int? readerId, DateTime? from, DateTime? to,
                                         string? sortOrder, int? pageNumber)
    {
        ViewData["ReaderId"] = readerId;
        ViewData["From"] = from?.ToString("yyyy-MM-dd");
        ViewData["To"] = to?.ToString("yyyy-MM-dd");
        ViewData["CurrentSort"] = sortOrder;
        ViewData["DateSortParm"] = sortOrder == "date_asc" ? "date_desc" : "date_asc";

        var logs = db.RecommendationLog.AsNoTracking();

        if (readerId != null)
            logs = logs.Where(l => l.ReaderId == readerId);
        if (from != null)
            logs = logs.Where(l => l.CreatedAt >= from.Value.Date);
        if (to != null)
        {
            var toExclusive = to.Value.Date.AddDays(1);      // „până la” include toată ziua
            logs = logs.Where(l => l.CreatedAt < toExclusive);
        }

        logs = sortOrder == "date_asc"
            ? logs.OrderBy(l => l.CreatedAt).ThenBy(l => l.ID)
            : logs.OrderByDescending(l => l.CreatedAt).ThenByDescending(l => l.ID);   // implicit: descrescător

        const int pageSize = 10;
        return View(await PaginatedList<RecommendationLog>.CreateAsync(logs, Math.Max(pageNumber ?? 1, 1), pageSize));
    }
}
