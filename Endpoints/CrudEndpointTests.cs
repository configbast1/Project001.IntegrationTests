namespace Project001.IntegrationTests.Endpoints;


/// <summary>HTTP-тесты /Posts: маршруты, антифорджери, коды ответов.</summary>
public class PostsEndpointTests : IClassFixture<MockedAppFactory>
{
    private readonly MockedAppFactory _factory;

    public PostsEndpointTests(MockedAppFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static Dictionary<string, string> Form(string id) => new()
    {
            ["PostId"] = id,
            ["BlogId"] = "blogid-value",
            ["Title"] = "title-value",
            ["Content"] = "content-value",
            ["CreatedAt"] = "2026-01-01T10:00:00Z",
    };

    // Страницы чтения без id или с несуществующим id отвечают 404.
    [Theory]
    [InlineData("/Posts/Details")]
    [InlineData("/Posts/Details/missing")]
    [InlineData("/Posts/Edit")]
    [InlineData("/Posts/Edit/missing")]
    [InlineData("/Posts/Delete")]
    [InlineData("/Posts/Delete/missing")]
    public async Task Get_ReturnsNotFound_ForMissingItem(string url)
    {
        _factory.Posts.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((Post?)null);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // POST Create с токеном: 302 на /Posts, сервис получил модель.
    [Fact]
    public async Task PostCreate_Redirects_AndCallsService()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Posts/Create", Form("new-1"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Posts", response.Headers.Location!.OriginalString);
        _factory.Posts.Verify(s => s.CreateAsync(It.Is<Post>(x => x.PostId == "new-1")), Times.Once);
    }

    // POST без антифорджери-токена отклоняется (400), сервис не вызывается.
    [Fact]
    public async Task PostCreate_WithoutToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync("/Posts/Create", new FormUrlEncodedContent(Form("new-1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _factory.Posts.Verify(s => s.CreateAsync(It.IsAny<Post>()), Times.Never);
    }

    // POST Edit: id в адресе и в форме разные — 404.
    [Fact]
    public async Task PostEdit_IdMismatch_ReturnsNotFound()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Posts/Edit/a", Form("b"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _factory.Posts.Verify(s => s.UpdateAsync(It.IsAny<Post>()), Times.Never);
    }

    // POST Edit с совпадающим id: 302 и обновление.
    [Fact]
    public async Task PostEdit_Valid_RedirectsAndUpdates()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Posts/Edit/item-1", Form("item-1"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Posts.Verify(s => s.UpdateAsync(It.Is<Post>(x => x.PostId == "item-1")), Times.Once);
    }

    // POST Delete существующей записи: 302 и удаление.
    [Fact]
    public async Task PostDelete_ExistingItem_Deletes()
    {
        _factory.Posts.Setup(s => s.GetByIdAsync("item-1")).ReturnsAsync(new Post { PostId = "item-1" });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Posts/Delete/item-1", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Posts.Verify(s => s.DeleteAsync("item-1"), Times.Once);
    }

    // POST Delete несуществующей записи: 302, удаления нет.
    [Fact]
    public async Task PostDelete_MissingItem_RedirectsWithoutDeleting()
    {
        _factory.Posts.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((Post?)null);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Posts/Delete/missing", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Posts.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    // POST Delete без id — 404.
    [Fact]
    public async Task PostDelete_WithoutId_ReturnsNotFound()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Posts/Delete", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>HTTP-тесты /Comments: маршруты, антифорджери, коды ответов.</summary>
public class CommentsEndpointTests : IClassFixture<MockedAppFactory>
{
    private readonly MockedAppFactory _factory;

    public CommentsEndpointTests(MockedAppFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static Dictionary<string, string> Form(string id) => new()
    {
            ["CommentId"] = id,
            ["PostId"] = "postid-value",
            ["Author"] = "author-value",
            ["Content"] = "content-value",
            ["CreatedAt"] = "2026-01-01T10:00:00Z",
    };

    // Страницы чтения без id или с несуществующим id отвечают 404.
    [Theory]
    [InlineData("/Comments/Details")]
    [InlineData("/Comments/Details/missing")]
    [InlineData("/Comments/Edit")]
    [InlineData("/Comments/Edit/missing")]
    [InlineData("/Comments/Delete")]
    [InlineData("/Comments/Delete/missing")]
    public async Task Get_ReturnsNotFound_ForMissingItem(string url)
    {
        _factory.Comments.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((Comment?)null);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // POST Create с токеном: 302 на /Comments, сервис получил модель.
    [Fact]
    public async Task PostCreate_Redirects_AndCallsService()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Comments/Create", Form("new-1"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Comments", response.Headers.Location!.OriginalString);
        _factory.Comments.Verify(s => s.CreateAsync(It.Is<Comment>(x => x.CommentId == "new-1")), Times.Once);
    }

    // POST без антифорджери-токена отклоняется (400), сервис не вызывается.
    [Fact]
    public async Task PostCreate_WithoutToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync("/Comments/Create", new FormUrlEncodedContent(Form("new-1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _factory.Comments.Verify(s => s.CreateAsync(It.IsAny<Comment>()), Times.Never);
    }

    // POST Edit: id в адресе и в форме разные — 404.
    [Fact]
    public async Task PostEdit_IdMismatch_ReturnsNotFound()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Comments/Edit/a", Form("b"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _factory.Comments.Verify(s => s.UpdateAsync(It.IsAny<Comment>()), Times.Never);
    }

    // POST Edit с совпадающим id: 302 и обновление.
    [Fact]
    public async Task PostEdit_Valid_RedirectsAndUpdates()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Comments/Edit/item-1", Form("item-1"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Comments.Verify(s => s.UpdateAsync(It.Is<Comment>(x => x.CommentId == "item-1")), Times.Once);
    }

    // POST Delete существующей записи: 302 и удаление.
    [Fact]
    public async Task PostDelete_ExistingItem_Deletes()
    {
        _factory.Comments.Setup(s => s.GetByIdAsync("item-1")).ReturnsAsync(new Comment { CommentId = "item-1" });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Comments/Delete/item-1", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Comments.Verify(s => s.DeleteAsync("item-1"), Times.Once);
    }

    // POST Delete несуществующей записи: 302, удаления нет.
    [Fact]
    public async Task PostDelete_MissingItem_RedirectsWithoutDeleting()
    {
        _factory.Comments.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((Comment?)null);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Comments/Delete/missing", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Comments.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    // POST Delete без id — 404.
    [Fact]
    public async Task PostDelete_WithoutId_ReturnsNotFound()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Comments/Delete", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>HTTP-тесты /Ratings: маршруты, антифорджери, коды ответов.</summary>
public class RatingsEndpointTests : IClassFixture<MockedAppFactory>
{
    private readonly MockedAppFactory _factory;

    public RatingsEndpointTests(MockedAppFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static Dictionary<string, string> Form(string id) => new()
    {
            ["RatingId"] = id,
            ["RecipeId"] = "recipeid-value",
            ["UserId"] = "7",
            ["Value"] = "5",
            ["CreatedAt"] = "2026-01-01T10:00:00Z",
    };

    // Страницы чтения без id или с несуществующим id отвечают 404.
    [Theory]
    [InlineData("/Ratings/Details")]
    [InlineData("/Ratings/Details/missing")]
    [InlineData("/Ratings/Edit")]
    [InlineData("/Ratings/Edit/missing")]
    [InlineData("/Ratings/Delete")]
    [InlineData("/Ratings/Delete/missing")]
    public async Task Get_ReturnsNotFound_ForMissingItem(string url)
    {
        _factory.Ratings.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((Rating?)null);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // POST Create с токеном: 302 на /Ratings, сервис получил модель.
    [Fact]
    public async Task PostCreate_Redirects_AndCallsService()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Ratings/Create", Form("new-1"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Ratings", response.Headers.Location!.OriginalString);
        _factory.Ratings.Verify(s => s.CreateAsync(It.Is<Rating>(x => x.RatingId == "new-1")), Times.Once);
    }

    // POST без антифорджери-токена отклоняется (400), сервис не вызывается.
    [Fact]
    public async Task PostCreate_WithoutToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync("/Ratings/Create", new FormUrlEncodedContent(Form("new-1")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _factory.Ratings.Verify(s => s.CreateAsync(It.IsAny<Rating>()), Times.Never);
    }

    // POST Edit: id в адресе и в форме разные — 404.
    [Fact]
    public async Task PostEdit_IdMismatch_ReturnsNotFound()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Ratings/Edit/a", Form("b"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        _factory.Ratings.Verify(s => s.UpdateAsync(It.IsAny<Rating>()), Times.Never);
    }

    // POST Edit с совпадающим id: 302 и обновление.
    [Fact]
    public async Task PostEdit_Valid_RedirectsAndUpdates()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Ratings/Edit/item-1", Form("item-1"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Ratings.Verify(s => s.UpdateAsync(It.Is<Rating>(x => x.RatingId == "item-1")), Times.Once);
    }

    // POST Delete существующей записи: 302 и удаление.
    [Fact]
    public async Task PostDelete_ExistingItem_Deletes()
    {
        _factory.Ratings.Setup(s => s.GetByIdAsync("item-1")).ReturnsAsync(new Rating { RatingId = "item-1" });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Ratings/Delete/item-1", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Ratings.Verify(s => s.DeleteAsync("item-1"), Times.Once);
    }

    // POST Delete несуществующей записи: 302, удаления нет.
    [Fact]
    public async Task PostDelete_MissingItem_RedirectsWithoutDeleting()
    {
        _factory.Ratings.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((Rating?)null);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Ratings/Delete/missing", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Ratings.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
    }

    // POST Delete без id — 404.
    [Fact]
    public async Task PostDelete_WithoutId_ReturnsNotFound()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Ratings/Delete", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
