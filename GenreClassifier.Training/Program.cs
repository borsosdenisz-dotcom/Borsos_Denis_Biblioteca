using Microsoft.ML;
using Microsoft.ML.Data;

var ml = new MLContext(seed: 1);
var sourcePath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.Combine(AppContext.BaseDirectory, "book-descriptions.csv");
var outputPath = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.Combine(AppContext.BaseDirectory, "GenreClassifier.zip");

IDataView data = ml.Data.LoadFromTextFile<BookText>(sourcePath,
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
ml.Model.Save(model, data.Schema, outputPath);
Console.WriteLine($"Model salvat în: {outputPath}");

var engine = ml.Model.CreatePredictionEngine<BookText, GenrePrediction>(model);
foreach (var description in new[]
{
    "Pe planeta deșertică Arrakis, Paul Atreides luptă pentru controlul mirodeniei și destinul omenirii.",
    "Un detectiv investighează dispariția misterioasă dintr-un oraș izolat."
})
{
    var prediction = engine.Predict(new BookText { Description = description });
    Console.WriteLine($"{description} => {prediction.Genre}: {prediction.Score.Max():P1}");
}

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
