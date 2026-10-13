using Stn.HackerNews.Api.Features.BestStories;
using Stn.HackerNews.Api.Infrastructure.HackerNews;

namespace Stn.HackerNews.UnitTests.Features.BestStories;

public class StoryDtoTests
{
    [Fact]
    public void FromItem_maps_hacker_news_fields_to_api_shape()
    {
        var item = new HackerNewsItem
        {
            Id = 21233041,
            Title = "A uBlock Origin update was rejected from the Chrome Web Store",
            Url = "https://github.com/uBlockOrigin/uBlock-issues/issues/745",
            By = "ismaildonmez",
            Time = 1570887781,
            Score = 1716,
            Descendants = 572
        };

        var dto = StoryDto.FromItem(item);

        Assert.Equal("A uBlock Origin update was rejected from the Chrome Web Store", dto.Title);
        Assert.Equal("https://github.com/uBlockOrigin/uBlock-issues/issues/745", dto.Uri);
        Assert.Equal("ismaildonmez", dto.PostedBy);
        Assert.Equal(new DateTimeOffset(2019, 10, 12, 13, 43, 1, TimeSpan.Zero), dto.Time);
        Assert.Equal(1716, dto.Score);
        Assert.Equal(572, dto.CommentCount);
    }

    [Fact]
    public void FromItem_without_url_links_to_hacker_news_discussion()
    {
        var item = new HackerNewsItem { Id = 42, Title = "Ask HN: Something", Url = null };

        var dto = StoryDto.FromItem(item);

        Assert.Equal("https://news.ycombinator.com/item?id=42", dto.Uri);
    }

    [Fact]
    public void FromItem_with_missing_optional_fields_uses_safe_defaults()
    {
        var item = new HackerNewsItem { Id = 1 };

        var dto = StoryDto.FromItem(item);

        Assert.Equal(string.Empty, dto.Title);
        Assert.Equal(string.Empty, dto.PostedBy);
        Assert.Equal(0, dto.CommentCount);
    }
}