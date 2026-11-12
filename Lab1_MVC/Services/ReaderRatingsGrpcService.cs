using BibliotecaGrpc;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Lab1_MVC.Data;
using Lab1_MVC.Models;
using Microsoft.EntityFrameworkCore;

namespace Lab1_MVC.Services;

public sealed class ReaderRatingsGrpcService(LibraryContext db) : ReaderRatings.ReaderRatingsBase
{
    public override async Task ListRatings(
        ReaderRequest request,
        IServerStreamWriter<RatingItem> responseStream,
        ServerCallContext context)
    {
        ValidateReaderId(request.ReaderId);

        var ratings = await db.Rating.AsNoTracking()
            .Include(rating => rating.Book)
            .Where(rating => rating.ReaderId == request.ReaderId)
            .OrderByDescending(rating => rating.CreatedAt)
            .ThenByDescending(rating => rating.ID)
            .ToListAsync(context.CancellationToken);

        foreach (var rating in ratings)
            await responseStream.WriteAsync(ToMessage(rating), context.CancellationToken);
    }

    public override async Task<RatingItem> GetRating(RatingId request, ServerCallContext context)
    {
        var rating = await FindRatingAsync(request.Id, context.CancellationToken);
        return ToMessage(rating);
    }

    public override async Task<RatingItem> AddRating(NewRating request, ServerCallContext context)
    {
        ValidateReaderId(request.ReaderId);
        ValidateStars(request.Stars);

        var book = await db.Book.FirstOrDefaultAsync(
            item => item.ID == request.BookId,
            context.CancellationToken);
        if (book is null)
            throw new RpcException(new Status(
                StatusCode.NotFound,
                $"Cartea cu ID {request.BookId} nu există."));

        var rating = new Rating
        {
            ReaderId = request.ReaderId,
            BookID = request.BookId,
            Book = book,
            Stars = request.Stars,
            CreatedAt = DateTime.UtcNow
        };
        db.Rating.Add(rating);
        await db.SaveChangesAsync(context.CancellationToken);
        return ToMessage(rating);
    }

    public override async Task<RatingItem> UpdateRating(
        UpdateRatingRequest request,
        ServerCallContext context)
    {
        ValidateStars(request.Stars);
        var rating = await FindRatingAsync(request.Id, context.CancellationToken);
        rating.Stars = request.Stars;
        await db.SaveChangesAsync(context.CancellationToken);
        return ToMessage(rating);
    }

    public override async Task<Empty> DeleteRating(RatingId request, ServerCallContext context)
    {
        var rating = await FindRatingAsync(request.Id, context.CancellationToken);
        db.Rating.Remove(rating);
        await db.SaveChangesAsync(context.CancellationToken);
        return new Empty();
    }

    private async Task<Rating> FindRatingAsync(int id, CancellationToken cancellationToken)
    {
        var rating = await db.Rating
            .Include(item => item.Book)
            .FirstOrDefaultAsync(item => item.ID == id, cancellationToken);
        return rating ?? throw new RpcException(new Status(
            StatusCode.NotFound,
            $"Evaluarea cu ID {id} nu există."));
    }

    private static RatingItem ToMessage(Rating rating) => new()
    {
        Id = rating.ID,
        BookId = rating.BookID,
        Title = rating.Book.Title,
        Stars = rating.Stars,
        CreatedAt = Timestamp.FromDateTime(
            DateTime.SpecifyKind(rating.CreatedAt, DateTimeKind.Utc))
    };

    private static void ValidateReaderId(int readerId)
    {
        if (readerId <= 0)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "reader_id trebuie să fie un număr pozitiv."));
    }

    private static void ValidateStars(int stars)
    {
        if (stars is < 1 or > 5)
            throw new RpcException(new Status(
                StatusCode.InvalidArgument,
                "stars trebuie să fie între 1 și 5."));
    }
}
