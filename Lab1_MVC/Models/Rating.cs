using System.ComponentModel.DataAnnotations;

namespace Lab1_MVC.Models;

public class Rating
{
    public int ID { get; set; }
    public int ReaderId { get; set; }          // cititorul din book-ratings.csv (nu este Customer)
    public int BookID { get; set; }
    public Book Book { get; set; } = null!;
    [Range(1, 5)]
    public int Stars { get; set; }
    public DateTime CreatedAt { get; set; }
}
