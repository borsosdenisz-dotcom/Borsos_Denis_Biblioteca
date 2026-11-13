using System.ComponentModel.DataAnnotations;
using Lab1_MVC.Rag;

namespace Lab1_MVC.Models;

public class AskViewModel
{
    [Display(Name = "Întrebarea")]
    public string Question { get; set; } = "";

    [Display(Name = "Documentul")]
    public string Source { get; set; } = "regulament";

    public string? Answer { get; set; }

    public List<DocumentHit> Sources { get; set; } = [];

    public long Milliseconds { get; set; }
}
