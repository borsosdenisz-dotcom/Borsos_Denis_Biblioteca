namespace Lab1_MVC.Models;

public record RecommendationDto(int BookId, string Title, float Score, bool Fallback);
