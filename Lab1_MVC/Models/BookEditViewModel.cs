using System.ComponentModel.DataAnnotations;

namespace Lab1_MVC.Models;

public class BookEditViewModel
{
    public int ID { get; set; }
    [Required, StringLength(200)] public string Title { get; set; } = "";
    [Range(1.0, 10000.0)] public decimal Price { get; set; }
    public string? Description { get; set; }
    public int? AuthorID { get; set; }
    public int? GenreID { get; set; }
}
