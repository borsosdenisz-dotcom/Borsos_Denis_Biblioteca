using System.ComponentModel.DataAnnotations;

namespace Lab1_MVC.Models;

// Doar câmpurile pe care formularul are voie să le trimită (protecție la overposting).
public class OrderFormViewModel
{
    public int OrderID { get; set; }

    [Display(Name = "Cititor")]
    [Range(1, int.MaxValue, ErrorMessage = "Alegeți un cititor.")]
    public int CustomerID { get; set; }

    [Display(Name = "Carte")]
    [Range(1, int.MaxValue, ErrorMessage = "Alegeți o carte.")]
    public int BookID { get; set; }

    [Display(Name = "Data comenzii")]
    [DataType(DataType.Date)]
    public DateTime OrderDate { get; set; } = DateTime.Today;
}
