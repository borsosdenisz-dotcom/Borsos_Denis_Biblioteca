using System.ComponentModel.DataAnnotations;
using BibliotecaGrpc;

namespace Biblioteca.Portal.Models;

public sealed class RatingsIndexViewModel
{
    [Display(Name = "ID cititor")]
    public int ReaderId { get; set; } = 1052;

    public List<RatingItem> Ratings { get; set; } = [];
    public string? Error { get; set; }
}

public sealed class RatingFormViewModel
{
    public int Id { get; set; }
    public int ReaderId { get; set; } = 1052;

    [Display(Name = "ID carte")]
    [Range(1, int.MaxValue)]
    public int BookId { get; set; }

    public string? Title { get; set; }

    [Display(Name = "Notă")]
    public int Stars { get; set; } = 5;

    public DateTime? CreatedAt { get; set; }
    public string? Error { get; set; }
}
