using Scalar.AspNetCore;

namespace Lab1_MVC.Api;

internal static class DevelopmentEndpoints
{
    public static void MapScalar(WebApplication app) => app.MapScalarApiReference();
}
