namespace Lab1_MVC.Models;

public record AuthorBookRow(int ID, string Title, string Genre, decimal Price, int Orders);

public class AuthorDetailsViewModel
{
    public Author Author { get; set; } = null!;
    public List<AuthorBookRow> Books { get; set; } = [];
    public int TotalOrders { get; set; }
    // [V4] B1: prețul mediu al cărților autorului, calculat în SQL (null dacă nu are cărți)
    public double? AveragePrice { get; set; }
}
