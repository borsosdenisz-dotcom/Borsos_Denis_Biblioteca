using Biblioteca.Portal.Models;
using BibliotecaGrpc;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Portal.Controllers;

public sealed class RatingsController(ReaderRatings.ReaderRatingsClient client) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int readerId = 1052)
    {
        var viewModel = new RatingsIndexViewModel { ReaderId = readerId };
        try
        {
            using var call = client.ListRatings(
                new ReaderRequest { ReaderId = readerId },
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: HttpContext.RequestAborted);
            await foreach (var rating in call.ResponseStream.ReadAllAsync(HttpContext.RequestAborted))
                viewModel.Ratings.Add(rating);
        }
        catch (RpcException exception)
        {
            viewModel.Error = ErrorMessage(exception);
        }

        return View(viewModel);
    }

    [HttpGet]
    public IActionResult Create(int readerId = 1052) =>
        View(new RatingFormViewModel { ReaderId = readerId });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RatingFormViewModel viewModel)
    {
        if (!ModelState.IsValid)
            return View(viewModel);

        try
        {
            await client.AddRatingAsync(
                new NewRating
                {
                    ReaderId = viewModel.ReaderId,
                    BookId = viewModel.BookId,
                    Stars = viewModel.Stars
                },
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index), new { readerId = viewModel.ReaderId });
        }
        catch (RpcException exception)
        {
            viewModel.Error = ErrorMessage(exception);
            return View(viewModel);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, int readerId = 1052)
    {
        var viewModel = new RatingFormViewModel { Id = id, ReaderId = readerId };
        try
        {
            var rating = await client.GetRatingAsync(
                new RatingId { Id = id },
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: HttpContext.RequestAborted);
            CopyRating(rating, viewModel);
        }
        catch (RpcException exception)
        {
            viewModel.Error = ErrorMessage(exception);
        }

        return View(viewModel);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(RatingFormViewModel viewModel)
    {
        ModelState.Remove(nameof(viewModel.BookId));
        if (!ModelState.IsValid)
            return View(viewModel);

        try
        {
            var rating = await client.UpdateRatingAsync(
                new UpdateRatingRequest { Id = viewModel.Id, Stars = viewModel.Stars },
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: HttpContext.RequestAborted);
            CopyRating(rating, viewModel);
            return RedirectToAction(nameof(Index), new { readerId = viewModel.ReaderId });
        }
        catch (RpcException exception)
        {
            viewModel.Error = ErrorMessage(exception);
            return View(viewModel);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id, int readerId = 1052)
    {
        var viewModel = new RatingFormViewModel { Id = id, ReaderId = readerId };
        try
        {
            var rating = await client.GetRatingAsync(
                new RatingId { Id = id },
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: HttpContext.RequestAborted);
            CopyRating(rating, viewModel);
        }
        catch (RpcException exception)
        {
            viewModel.Error = ErrorMessage(exception);
        }

        return View(viewModel);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(RatingFormViewModel viewModel)
    {
        try
        {
            await client.DeleteRatingAsync(
                new RatingId { Id = viewModel.Id },
                deadline: DateTime.UtcNow.AddSeconds(5),
                cancellationToken: HttpContext.RequestAborted);
            return RedirectToAction(nameof(Index), new { readerId = viewModel.ReaderId });
        }
        catch (RpcException exception)
        {
            viewModel.Error = ErrorMessage(exception);
            return View("Delete", viewModel);
        }
    }

    private static string ErrorMessage(RpcException exception) =>
        $"{exception.StatusCode}: {exception.Status.Detail}";

    private static void CopyRating(RatingItem rating, RatingFormViewModel viewModel)
    {
        viewModel.Id = rating.Id;
        viewModel.BookId = rating.BookId;
        viewModel.Title = rating.Title;
        viewModel.Stars = rating.Stars;
        viewModel.CreatedAt = rating.CreatedAt.ToDateTime().ToLocalTime();
    }
}
