namespace Project001.IntegrationTests.EndToEnd;


/// <summary>Сквозной CRUD для Recipes через настоящий RecipeService и in-memory хранилище.</summary>
public class RecipesEndToEndTests : IClassFixture<InMemoryAppFactory>
{
    private readonly InMemoryAppFactory _factory;

    public RecipesEndToEndTests(InMemoryAppFactory factory) => _factory = factory;

    private static Dictionary<string, string> Form(string id) => new()
    {
            ["RecipeId"] = id,
            ["Title"] = "title-value",
            ["Description"] = "description-value",
            ["Ingredients"] = "ingredients-value",
            ["Instructions"] = "instructions-value",
            ["CreatedAt"] = "2026-01-01T10:00:00Z",
    };

    // Создание через HTTP сохраняет запись в хранилище.
    [Fact]
    public async Task Create_StoresItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Recipes/Create", Form(id));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(await _factory.Recipes.GetByIdAsync(id));
    }

    // Редактирование через HTTP меняет сохранённую запись.
    [Fact]
    public async Task Edit_UpdatesStoredItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        await _factory.Recipes.CreateAsync(new Recipe { RecipeId = id });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Recipes/Edit/" + id, Form(id));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var stored = await _factory.Recipes.GetByIdAsync(id);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), stored!.CreatedAt.ToUniversalTime());
    }

    // Удаление через HTTP убирает запись из хранилища.
    [Fact]
    public async Task Delete_RemovesStoredItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        await _factory.Recipes.CreateAsync(new Recipe { RecipeId = id });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Recipes/Delete/" + id, new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await _factory.Recipes.ExistsAsync(id));
    }
}

/// <summary>Сквозной CRUD для Posts через настоящий PostService и in-memory хранилище.</summary>
public class PostsEndToEndTests : IClassFixture<InMemoryAppFactory>
{
    private readonly InMemoryAppFactory _factory;

    public PostsEndToEndTests(InMemoryAppFactory factory) => _factory = factory;

    private static Dictionary<string, string> Form(string id) => new()
    {
            ["PostId"] = id,
            ["BlogId"] = "blogid-value",
            ["Title"] = "title-value",
            ["Content"] = "content-value",
            ["CreatedAt"] = "2026-01-01T10:00:00Z",
    };

    // Создание через HTTP сохраняет запись в хранилище.
    [Fact]
    public async Task Create_StoresItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Posts/Create", Form(id));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(await _factory.Posts.GetByIdAsync(id));
    }

    // Редактирование через HTTP меняет сохранённую запись.
    [Fact]
    public async Task Edit_UpdatesStoredItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        await _factory.Posts.CreateAsync(new Post { PostId = id });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Posts/Edit/" + id, Form(id));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var stored = await _factory.Posts.GetByIdAsync(id);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), stored!.CreatedAt.ToUniversalTime());
    }

    // Удаление через HTTP убирает запись из хранилища.
    [Fact]
    public async Task Delete_RemovesStoredItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        await _factory.Posts.CreateAsync(new Post { PostId = id });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Posts/Delete/" + id, new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await _factory.Posts.ExistsAsync(id));
    }
}

/// <summary>Сквозной CRUD для Comments через настоящий CommentService и in-memory хранилище.</summary>
public class CommentsEndToEndTests : IClassFixture<InMemoryAppFactory>
{
    private readonly InMemoryAppFactory _factory;

    public CommentsEndToEndTests(InMemoryAppFactory factory) => _factory = factory;

    private static Dictionary<string, string> Form(string id) => new()
    {
            ["CommentId"] = id,
            ["PostId"] = "postid-value",
            ["Author"] = "author-value",
            ["Content"] = "content-value",
            ["CreatedAt"] = "2026-01-01T10:00:00Z",
    };

    // Создание через HTTP сохраняет запись в хранилище.
    [Fact]
    public async Task Create_StoresItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Comments/Create", Form(id));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(await _factory.Comments.GetByIdAsync(id));
    }

    // Редактирование через HTTP меняет сохранённую запись.
    [Fact]
    public async Task Edit_UpdatesStoredItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        await _factory.Comments.CreateAsync(new Comment { CommentId = id });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Comments/Edit/" + id, Form(id));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var stored = await _factory.Comments.GetByIdAsync(id);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), stored!.CreatedAt.ToUniversalTime());
    }

    // Удаление через HTTP убирает запись из хранилища.
    [Fact]
    public async Task Delete_RemovesStoredItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        await _factory.Comments.CreateAsync(new Comment { CommentId = id });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Comments/Delete/" + id, new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await _factory.Comments.ExistsAsync(id));
    }
}

/// <summary>Сквозной CRUD для Ratings через настоящий RatingService и in-memory хранилище.</summary>
public class RatingsEndToEndTests : IClassFixture<InMemoryAppFactory>
{
    private readonly InMemoryAppFactory _factory;

    public RatingsEndToEndTests(InMemoryAppFactory factory) => _factory = factory;

    private static Dictionary<string, string> Form(string id) => new()
    {
            ["RatingId"] = id,
            ["RecipeId"] = "recipeid-value",
            ["UserId"] = "7",
            ["Value"] = "5",
            ["CreatedAt"] = "2026-01-01T10:00:00Z",
    };

    // Создание через HTTP сохраняет запись в хранилище.
    [Fact]
    public async Task Create_StoresItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Ratings/Create", Form(id));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(await _factory.Ratings.GetByIdAsync(id));
    }

    // Редактирование через HTTP меняет сохранённую запись.
    [Fact]
    public async Task Edit_UpdatesStoredItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        await _factory.Ratings.CreateAsync(new Rating { RatingId = id });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Ratings/Edit/" + id, Form(id));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var stored = await _factory.Ratings.GetByIdAsync(id);
        Assert.Equal(new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc), stored!.CreatedAt.ToUniversalTime());
    }

    // Удаление через HTTP убирает запись из хранилища.
    [Fact]
    public async Task Delete_RemovesStoredItem()
    {
        var id = "e2e-" + Guid.NewGuid().ToString("N");
        await _factory.Ratings.CreateAsync(new Rating { RatingId = id });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Ratings/Delete/" + id, new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.False(await _factory.Ratings.ExistsAsync(id));
    }
}
