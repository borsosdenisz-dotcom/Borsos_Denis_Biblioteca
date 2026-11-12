using Biblioteca.Portal.Models;
using BibliotecaGrpc;
using Grpc.Core;
using Microsoft.AspNetCore.Mvc;

namespace Biblioteca.Portal.Controllers;

public class RecommendationsController(Recommender.RecommenderClient client) : Controller
{
    [HttpGet]
    public IActionResult Index() => View(new RecommendationsViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(RecommendationsViewModel viewModel)
    {
        try
        {
            var reply = await client.RecommendAsync(
                new RecommendRequest
                {
                    ReaderId = viewModel.ReaderId,
                    Top = viewModel.Top
                },
                deadline: DateTime.UtcNow.AddSeconds(5));
            viewModel.Books = reply.Books.ToList();
        }
        catch (RpcException exception)
        {
            viewModel.Error = $"{exception.StatusCode}: {exception.Status.Detail}";
        }

        return View(viewModel);
    }
}
