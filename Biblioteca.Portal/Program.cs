using BibliotecaGrpc;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddGrpcClient<Recommender.RecommenderClient>(options =>
{
    options.Address = new Uri(builder.Configuration["Biblioteca:GrpcAddress"]
        ?? throw new InvalidOperationException("Lipsește Biblioteca:GrpcAddress."));
});
builder.Services.AddGrpcClient<ReaderRatings.ReaderRatingsClient>(options =>
{
    options.Address = new Uri(builder.Configuration["Biblioteca:GrpcAddress"]
        ?? throw new InvalidOperationException("Lipsește Biblioteca:GrpcAddress."));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
