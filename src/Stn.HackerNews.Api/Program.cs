using Stn.HackerNews.Api.Features.BestStories;
using Stn.HackerNews.Api.Infrastructure.Caching;
using Stn.HackerNews.Api.Infrastructure.HackerNews;
using Stn.HackerNews.Api.Infrastructure.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddHackerNewsClient(builder.Configuration)
    .AddAppCaching(builder.Configuration)
    .AddApiRateLimiting(builder.Configuration);

builder.Services.AddBestStoriesFeature(builder.Configuration);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseSwagger();
app.UseSwaggerUI();

var api = app.MapGroup("/api")
    .RequireRateLimiting(RateLimitingExtensions.PerClientPolicy);

api.MapBestStoriesEndpoints();

app.MapHealthChecks("/health");

app.Run();
public partial class Program { }