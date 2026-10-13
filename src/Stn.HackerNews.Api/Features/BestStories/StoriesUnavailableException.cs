namespace Stn.HackerNews.Api.Features.BestStories;

public sealed class StoriesUnavailableException(string message, Exception? inner = null)
    : Exception(message, inner);