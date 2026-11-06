namespace Lab1_MVC.Models;

public record StarCount(int Stars, int Count);
public record AuthorAverage(string Author, double Average, int Count);

// S2: datele pentru Admin/Dashboard (toate calculate în SQL)
public class DashboardViewModel
{
    public int RatingCount { get; set; }
    public int ReaderCount { get; set; }
    public double? AverageStars { get; set; }
    public List<StarCount> Distribution { get; set; } = [];
    public List<AuthorAverage> AuthorAverages { get; set; } = [];   // [V] B1: media pe autor
}
