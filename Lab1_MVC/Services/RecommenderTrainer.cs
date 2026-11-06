using Lab1_MVC.Data;
using Lab1_MVC.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using Microsoft.ML.Trainers;

namespace Lab1_MVC.Services;

public class RecommenderTrainer(LibraryContext db, IWebHostEnvironment env)
{
    public async Task<(double Rmse, int Count)> TrainAsync()
    {
        var ratings = await db.Rating.AsNoTracking()
            .Select(r => new BookRating { UserId = r.ReaderId, BookId = r.BookID, Rating = r.Stars })
            .ToListAsync();

        var ml = new MLContext(seed: 1);
        IDataView data = ml.Data.LoadFromEnumerable(ratings);
        var split = ml.Data.TrainTestSplit(data, testFraction: 0.2);

        var options = new MatrixFactorizationTrainer.Options
        {
            MatrixColumnIndexColumnName = "UserKey",
            MatrixRowIndexColumnName = "BookKey",
            LabelColumnName = "Label",
            NumberOfIterations = 20,
            ApproximationRank = 16
        };

        var pipeline = ml.Transforms.Conversion.MapValueToKey("UserKey", "UserId")
            .Append(ml.Transforms.Conversion.MapValueToKey("BookKey", "BookId"))
            .Append(ml.Recommendation().Trainers.MatrixFactorization(options));

        var model = pipeline.Fit(split.TrainSet);
        var metrics = ml.Regression.Evaluate(model.Transform(split.TestSet));

        var path = Path.Combine(env.ContentRootPath, "MLModels", "BookRecommender.zip");
        ml.Model.Save(model, data.Schema, path);
        return (metrics.RootMeanSquaredError, ratings.Count);
    }
}
