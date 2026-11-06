using Microsoft.ML.Data;

namespace Lab1_MVC.Models;

public class GenrePrediction
{
    [ColumnName("PredictedLabel")] public string Genre { get; set; } = "";
    public float[] Score { get; set; } = [];
}
