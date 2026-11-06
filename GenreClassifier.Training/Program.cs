using Microsoft.ML;
using Microsoft.ML.Data;

var ml = new MLContext(seed: 1);
IDataView data = ml.Data.LoadFromTextFile<BookText>("book-descriptions.csv",
    hasHeader: true, separatorChar: ',', allowQuoting: true);
var split = ml.Data.TrainTestSplit(data, testFraction: 0.2);

var pipeline = ml.Transforms.Conversion.MapValueToKey("Label", nameof(BookText.Genre))
    .Append(ml.Transforms.Text.FeaturizeText("Features", nameof(BookText.Description)))
    .Append(ml.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
    .Append(ml.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

var model = pipeline.Fit(split.TrainSet);
var m = ml.MulticlassClassification.Evaluate(model.Transform(split.TestSet));
Console.WriteLine($"MicroAccuracy: {m.MicroAccuracy:P1}");
Console.WriteLine($"MacroAccuracy: {m.MacroAccuracy:P1}");
Console.WriteLine(m.ConfusionMatrix.GetFormattedConfusionTable());
ml.Model.Save(model, data.Schema, "GenreClassifier.zip");

public class BookText
{
    [LoadColumn(0)] public string Description { get; set; } = "";
    [LoadColumn(1)] public string Genre { get; set; } = "";
}

public class GenrePrediction
{
    [ColumnName("PredictedLabel")] public string Genre { get; set; } = "";
    public float[] Score { get; set; } = [];
}
