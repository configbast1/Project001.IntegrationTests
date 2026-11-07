namespace Project001.IntegrationTests.Endpoints;

/// <summary>Главная страница, навигация и несуществующие адреса.</summary>
public class HomeEndpointTests : IClassFixture<MockedAppFactory>
{
    private readonly MockedAppFactory _factory;

    public HomeEndpointTests(MockedAppFactory factory) => _factory = factory;

    // Главная доступна по всем адресам маршрута по умолчанию.
    [Theory]
    [InlineData("/")]
    [InlineData("/Home")]
    [InlineData("/Home/Index")]
    public async Task Home_ReturnsOk(string url)
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // Ответ — HTML с названием сайта.
    [Fact]
    public async Task Home_ReturnsHtmlWithSiteName()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("Recipe.Site", await response.ReadHtmlAsync());
    }

    // Гость видит в навигации ссылки на разделы, регистрацию и вход.
    [Theory]
    [InlineData("href=\"/Blogs\"")]
    [InlineData("href=\"/Recipes\"")]
    [InlineData("href=\"/Preferences\"")]
    [InlineData("href=\"/Form\"")]
    [InlineData("href=\"/Account/Login\"")]
    public async Task Home_GuestNavigationContainsLink(string link)
    {
        using var client = _factory.CreateClient();

        Assert.Contains(link, await client.GetStringAsync("/"));
    }

    // Несуществующие адреса — 404.
    [Theory]
    [InlineData("/DefinitelyMissing")]
    [InlineData("/Home/NoSuchAction")]
    [InlineData("/Recipes/NoSuchAction/1")]
    public async Task UnknownUrl_ReturnsNotFound(string url)
    {
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }

    // Язык страницы выбирается по Accept-Language, по умолчанию — русский.
    [Theory]
    [InlineData("uk-UA", "lang=\"uk\"")]
    [InlineData("ru-RU", "lang=\"ru\"")]
    [InlineData("de-DE", "lang=\"ru\"")]
    public async Task Home_UsesRequestCulture(string acceptLanguage, string expectedLang)
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);

        Assert.Contains(expectedLang, await client.GetStringAsync("/"));
    }

    // Культуру можно задать параметром ?culture=.
    [Fact]
    public async Task Home_CultureQueryString_SwitchesLanguage()
    {
        using var client = _factory.CreateClient();

        Assert.Contains("lang=\"uk\"", await client.GetStringAsync("/?culture=uk-UA"));
    }
}

/// <summary>/Recipes: список, детали, создание, редактирование, удаление.</summary>
public class RecipesEndpointTests : IClassFixture<MockedAppFactory>
{
    private readonly MockedAppFactory _factory;

    public RecipesEndpointTests(MockedAppFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static Recipe Make(string id, string title) => new()
    {
        RecipeId = id, Title = title, Description = "Description", Ingredients = "Water", Instructions = "Boil"
    };

    private static Dictionary<string, string> Form(string id) => new()
    {
        ["RecipeId"] = id,
        ["Title"] = "Borscht",
        ["Description"] = "Soup",
        ["Ingredients"] = "Beet",
        ["Instructions"] = "Cook",
        ["CreatedAt"] = "2026-01-01T10:00:00Z"
    };

    // Список показывает названия всех рецептов, сервис вызывается один раз.
    [Fact]
    public async Task Index_RendersAllRecipes()
    {
        _factory.Recipes.Setup(s => s.GetAllAsync())
            .ReturnsAsync(new List<Recipe> { Make("r1", "Test Pasta"), Make("r2", "Test Soup") });
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/Recipes");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Test Pasta", html);
        Assert.Contains("Test Soup", html);
        _factory.Recipes.Verify(s => s.GetAllAsync(), Times.Once);
    }

    // Пустой список — блок «пока пусто».
    [Fact]
    public async Task Index_EmptyList_RendersEmptyState()
    {
        _factory.Recipes.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Recipe>());
        using var client = _factory.CreateClient();

        Assert.Contains("panel empty", await client.GetStringAsync("/Recipes"));
    }

    // Страница содержит форму создания с антифорджери-токеном.
    [Fact]
    public async Task Index_ContainsCreateFormWithToken()
    {
        _factory.Recipes.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Recipe>());
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/Recipes");

        Assert.Contains("action=\"/Recipes/Create\"", html);
        Assert.Contains("__RequestVerificationToken", html);
    }

    // Детали доступны и по ?id=, и по /Details/{id}.
    [Theory]
    [InlineData("/Recipes/Details?id=r1")]
    [InlineData("/Recipes/Details/r1")]
    public async Task Details_ExistingRecipe_ReturnsOk(string url)
    {
        _factory.Recipes.Setup(s => s.GetByIdAsync("r1")).ReturnsAsync(Make("r1", "Test Soup"));
        using var client = _factory.CreateClient();

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Test Soup", await response.ReadHtmlAsync());
    }

    // Отсутствующий рецепт или пустой id — 404 на всех страницах чтения.
    [Theory]
    [InlineData("/Recipes/Details")]
    [InlineData("/Recipes/Details/missing")]
    [InlineData("/Recipes/Edit")]
    [InlineData("/Recipes/Edit/missing")]
    [InlineData("/Recipes/Delete")]
    [InlineData("/Recipes/Delete/missing")]
    public async Task Get_MissingRecipe_ReturnsNotFound(string url)
    {
        _factory.Recipes.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((Recipe?)null);
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }

    // POST Create: 302 на /Recipes, сервис получил рецепт с данными формы.
    [Fact]
    public async Task PostCreate_RedirectsToIndexAndSaves()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Recipes/Create", Form("r-new"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Recipes", response.LocationOf());
        _factory.Recipes.Verify(s => s.CreateAsync(It.Is<Recipe>(r => r.Title == "Borscht" && r.Ingredients == "Beet")), Times.Once);
    }

    // POST без токена или с поддельным токеном — 400.
    [Theory]
    [InlineData(null)]
    [InlineData("forged-token")]
    public async Task PostCreate_WithoutValidToken_ReturnsBadRequest(string? token)
    {
        using var client = _factory.CreateNoRedirectClient();
        var form = Form("r-new");
        if (token != null)
        {
            form["__RequestVerificationToken"] = token;
        }

        var response = await client.PostAsync("/Recipes/Create", new FormUrlEncodedContent(form));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _factory.Recipes.Verify(s => s.CreateAsync(It.IsAny<Recipe>()), Times.Never);
    }

    // POST Edit с разными id — 404.
    [Fact]
    public async Task PostEdit_IdMismatch_ReturnsNotFound()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Recipes/Edit/a", Form("b"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // POST Edit с совпадающим id — 302 и обновление.
    [Fact]
    public async Task PostEdit_Valid_UpdatesAndRedirects()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Recipes/Edit/r1", Form("r1"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Recipes.Verify(s => s.UpdateAsync(It.Is<Recipe>(r => r.RecipeId == "r1")), Times.Once);
    }

    // POST Delete: существующий удаляется, отсутствующий — нет; в обоих случаях 302.
    [Theory]
    [InlineData("r1", true)]
    [InlineData("missing", false)]
    public async Task PostDelete_DeletesOnlyExisting(string id, bool exists)
    {
        _factory.Recipes.Setup(s => s.GetByIdAsync("r1")).ReturnsAsync(Make("r1", "Soup"));
        _factory.Recipes.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((Recipe?)null);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Recipes/Delete/" + id, new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        _factory.Recipes.Verify(s => s.DeleteAsync(id), exists ? Times.Once() : Times.Never());
    }

    // POST Delete без id — 404.
    [Fact]
    public async Task PostDelete_WithoutId_ReturnsNotFound()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Recipes/Delete", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}

/// <summary>/Blogs: список, авторизация на создание, редактирование и удаление.</summary>
public class BlogsEndpointTests : IClassFixture<MockedAppFactory>
{
    private readonly MockedAppFactory _factory;

    public BlogsEndpointTests(MockedAppFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private static Dictionary<string, string> Form(string id) => new()
    {
        ["BlogId"] = id,
        ["Title"] = "My Blog",
        ["Description"] = "About food",
        ["CreatedAt"] = "2026-01-01T10:00:00Z"
    };

    // Список блогов показывает названия и описания.
    [Fact]
    public async Task Index_RendersBlogs()
    {
        _factory.Blogs.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Blog>
        {
            new() { BlogId = "b1", Title = "Kitchen Notes", Description = "Daily cooking", CreatedAt = DateTime.UtcNow }
        });
        using var client = _factory.CreateClient();

        var html = await (await client.GetAsync("/Blogs")).ReadHtmlAsync();

        Assert.Contains("Kitchen Notes", html);
        Assert.Contains("Daily cooking", html);
    }

    // Пустой список — блок «пока пусто».
    [Fact]
    public async Task Index_Empty_RendersEmptyState()
    {
        _factory.Blogs.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Blog>());
        using var client = _factory.CreateClient();

        Assert.Contains("panel empty", await client.GetStringAsync("/Blogs"));
    }

    // У каждого блога есть формы редактирования и удаления.
    [Fact]
    public async Task Index_RendersEditAndDeleteForms()
    {
        _factory.Blogs.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<Blog> { new() { BlogId = "b1", Title = "T" } });
        using var client = _factory.CreateClient();

        var html = await client.GetStringAsync("/Blogs");

        Assert.Contains("action=\"/Blogs/Edit/b1\"", html);
        Assert.Contains("action=\"/Blogs/Delete/b1\"", html);
    }

    // Отсутствующий блог или пустой id — 404.
    [Theory]
    [InlineData("/Blogs/Details")]
    [InlineData("/Blogs/Details/missing")]
    [InlineData("/Blogs/Edit")]
    [InlineData("/Blogs/Edit/missing")]
    [InlineData("/Blogs/Delete")]
    [InlineData("/Blogs/Delete/missing")]
    public async Task Get_MissingBlog_ReturnsNotFound(string url)
    {
        _factory.Blogs.Setup(s => s.GetByIdAsync("missing")).ReturnsAsync((Blog?)null);
        using var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
    }

    // Гость, открывший создание блога, уходит на вход с адресом возврата.
    [Fact]
    public async Task GetCreate_Guest_RedirectsToLogin()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync("/Blogs/Create");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.LocationOf());
        Assert.Contains("ReturnUrl=%2FBlogs%2FCreate", response.LocationOf());
    }

    // Гость не может создать блог даже POST-запросом.
    [Fact]
    public async Task PostCreate_Guest_RedirectsToLoginWithoutSaving()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Blogs/Create", Form("b-new"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.LocationOf());
        _factory.Blogs.Verify(s => s.CreateAsync(It.IsAny<Blog>()), Times.Never);
    }

    // Вошедший пользователь создаёт блог: 302 на /Blogs.
    [Fact]
    public async Task PostCreate_AuthenticatedUser_SavesBlog()
    {
        _factory.SetupUser();
        using var client = _factory.CreateNoRedirectClient();
        await client.LoginAsync();

        var response = await client.PostFormWithTokenAsync("/Blogs/Create", Form("b-new"));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Blogs", response.LocationOf());
        _factory.Blogs.Verify(s => s.CreateAsync(It.Is<Blog>(b => b.Title == "My Blog")), Times.Once);
    }

    // Редактирование: несовпадающий id — 404, совпадающий — 302 и обновление.
    [Theory]
    [InlineData("b1", "b1", HttpStatusCode.Redirect)]
    [InlineData("b1", "b2", HttpStatusCode.NotFound)]
    public async Task PostEdit_ChecksId(string routeId, string formId, HttpStatusCode expected)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Blogs/Edit/" + routeId, Form(formId));

        Assert.Equal(expected, response.StatusCode);
        _factory.Blogs.Verify(s => s.UpdateAsync(It.IsAny<Blog>()),
            expected == HttpStatusCode.Redirect ? Times.Once() : Times.Never());
    }

    // Удаление существующего блога.
    [Fact]
    public async Task PostDelete_ExistingBlog_Deletes()
    {
        _factory.Blogs.Setup(s => s.GetByIdAsync("b1")).ReturnsAsync(new Blog { BlogId = "b1" });
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Blogs/Delete/b1", new Dictionary<string, string>());

        Assert.Equal("/Blogs", response.LocationOf());
        _factory.Blogs.Verify(s => s.DeleteAsync("b1"), Times.Once);
    }

    // Удаление без токена — 400.
    [Fact]
    public async Task PostDelete_WithoutToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync("/Blogs/Delete/b1", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        _factory.Blogs.Verify(s => s.DeleteAsync(It.IsAny<string>()), Times.Never);
    }
}
