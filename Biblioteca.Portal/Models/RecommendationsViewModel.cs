using System.ComponentModel.DataAnnotations;
using BibliotecaGrpc;

namespace Biblioteca.Portal.Models;

public class RecommendationsViewModel
{
    [Display(Name = "ID cititor")]
    public int ReaderId { get; set; } = 1052;

    [Display(Name = "Câte cărți")]
    public int Top { get; set; } = 5;

    public List<BookScore> Books { get; set; } = [];
    public string? Error { get; set; }
}
