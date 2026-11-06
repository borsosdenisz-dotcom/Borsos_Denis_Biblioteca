namespace Lab1_MVC.Models;

// S1: un rând pentru fiecare apel al API-ului de recomandări (inclusiv cold start)
public class RecommendationLog
{
    public int ID { get; set; }
    public int ReaderId { get; set; }
    public DateTime CreatedAt { get; set; }      // UTC
    public string BookIds { get; set; } = "";    // ex. "16,17,18"
}
