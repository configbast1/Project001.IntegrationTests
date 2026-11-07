using Project001.Models;

namespace Project001.IntegrationTests.Endpoints;

/// <summary>/Account: вход по cookie, адрес возврата, выход.</summary>
public class AccountEndpointTests : IClassFixture<MockedAppFactory>
{
    private const string AuthCookie = ".AspNetCore.Cookies";
    private readonly MockedAppFactory _factory;

    public AccountEndpointTests(MockedAppFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _factory.SetupUser();
    }

    // Страница входа содержит поля формы и токен.
    [Theory]
    [InlineData("name=\"Email\"")]
    [InlineData("name=\"Password\"")]
    [InlineData("name=\"RememberMe\"")]
    [InlineData("name=\"__RequestVerificationToken\"")]
    public async Task GetLogin_RendersFormField(string field)
    {
        using var client = _factory.CreateClient();

        Assert.Contains(field, await client.GetStringAsync("/Account/Login"));
    }

    // Адрес возврата попадает в скрытое поле формы.
    [Fact]
    public async Task GetLogin_KeepsReturnUrlInHiddenField()
    {
        using var client = _factory.CreateClient();

        var html = await (await client.GetAsync("/Account/Login?returnUrl=%2FBlogs")).ReadHtmlAsync();

        Assert.Contains("value=\"/Blogs\"", html);
    }

    // /Account/Register перенаправляет на /Form.
    [Fact]
    public async Task GetRegister_RedirectsToForm()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.GetAsync("/Account/Register");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Form", response.LocationOf());
    }

    // Верные данные: 302 на главную и cookie аутентификации.
    [Fact]
    public async Task PostLogin_ValidCredentials_SetsAuthCookie()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.LoginAsync();

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.LocationOf());
        Assert.NotNull(response.GetSetCookie(AuthCookie));
    }

    // Почта ищется в хранилище так, как её ввёл пользователь.
    [Fact]
    public async Task PostLogin_LooksUpUserByEmail()
    {
        using var client = _factory.CreateNoRedirectClient();

        await client.LoginAsync();

        _factory.Store.Verify(s => s.FindByEmailAsync(TestUser.Email, It.IsAny<CancellationToken>()), Times.Once);
    }

    // После входа на главной приветствие и кнопка выхода.
    [Fact]
    public async Task AfterLogin_HomeShowsGreetingAndLogout()
    {
        using var client = _factory.CreateNoRedirectClient();
        await client.LoginAsync();

        var html = await (await client.GetAsync("/")).ReadHtmlAsync();

        Assert.Contains("Привет, Anna!", html);
        Assert.Contains("logout-button", html);
        Assert.DoesNotContain("href=\"/Account/Login\"", html);
    }

    // У пользователя без имени в приветствии показывается почта.
    [Fact]
    public async Task AfterLogin_UserWithoutName_GreetsByEmail()
    {
        _factory.SetupUser(email: "noname@example.com", name: "");
        using var client = _factory.CreateNoRedirectClient();
        await client.LoginAsync(email: "noname@example.com");

        var html = await (await client.GetAsync("/")).ReadHtmlAsync();

        Assert.Contains("Привет, noname@example.com!", html);
    }

    // «Запомнить меня» делает cookie постоянной (с датой истечения).
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task PostLogin_RememberMe_ControlsCookieExpiry(bool remember, bool hasExpires)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.LoginAsync(remember: remember);

        var cookie = response.GetSetCookie(AuthCookie)!;
        Assert.Equal(hasExpires, cookie.Contains("expires=", StringComparison.OrdinalIgnoreCase));
    }

    // Неверный пароль и неизвестная почта дают одно и то же сообщение.
    [Theory]
    [InlineData(TestUser.Email, "wrong-password")]
    [InlineData("unknown@example.com", TestUser.Password)]
    public async Task PostLogin_BadCredentials_ShowsGenericError(string email, string password)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Неверный email или пароль", await response.ReadHtmlAsync());
        Assert.Null(response.GetSetCookie(AuthCookie));
    }

    // Пустые или некорректные поля: форма с ошибками, хранилище не вызывается.
    [Theory]
    [InlineData("", "secret123")]
    [InlineData("not-an-email", "secret123")]
    [InlineData(TestUser.Email, "")]
    public async Task PostLogin_InvalidInput_DoesNotQueryStore(string email, string password)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.LoginAsync(email, password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _factory.Store.Verify(s => s.FindByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Локальный адрес возврата соблюдается, внешний — заменяется на главную.
    [Theory]
    [InlineData("/Recipes", "/Recipes")]
    [InlineData("https://evil.example.com/", "/")]
    [InlineData("//evil.example.com", "/")]
    public async Task PostLogin_ReturnUrl_OnlyLocalAllowed(string returnUrl, string expectedLocation)
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Account/Login", new Dictionary<string, string>
        {
            ["Email"] = TestUser.Email,
            ["Password"] = TestUser.Password,
            ["ReturnUrl"] = returnUrl
        });

        Assert.Equal(expectedLocation, response.LocationOf());
    }

    // Вход без антифорджери-токена — 400.
    [Fact]
    public async Task PostLogin_WithoutToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = TestUser.Email,
            ["Password"] = TestUser.Password
        }));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // Выход удаляет cookie и возвращает гостевую навигацию.
    [Fact]
    public async Task PostLogout_EndsSession()
    {
        using var client = _factory.CreateNoRedirectClient();
        await client.LoginAsync();

        var logout = await client.PostFormWithTokenAsync("/Account/Logout", new Dictionary<string, string>());
        var html = await (await client.GetAsync("/")).ReadHtmlAsync();

        Assert.Equal("/", logout.LocationOf());
        Assert.DoesNotContain("Привет, Anna!", html);
        Assert.Contains("href=\"/Account/Login\"", html);
    }

    // Выход без токена — 400, сессия остаётся.
    [Fact]
    public async Task PostLogout_WithoutToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateNoRedirectClient();
        await client.LoginAsync();

        var response = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Привет, Anna!", await (await client.GetAsync("/")).ReadHtmlAsync());
    }
}

/// <summary>/Form: регистрация с проверками и автоматическим входом.</summary>
public class FormEndpointTests : IClassFixture<MockedAppFactory>
{
    private readonly MockedAppFactory _factory;

    public FormEndpointTests(MockedAppFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
        _factory.Store.Setup(s => s.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _factory.Store.Setup(s => s.SaveAsync(It.IsAny<RegistrationViewModel>(), It.IsAny<CancellationToken>())).ReturnsAsync("new-user");
    }

    private static Dictionary<string, string> ValidForm() => new()
    {
        ["name"] = "Anna",
        ["lastName"] = "Ivanova",
        ["email"] = "anna@example.com",
        ["password"] = "secret123",
        ["confirmPassword"] = "secret123",
        ["phone"] = "+380501112233",
        ["country"] = "Украина",
        ["agreement"] = "true"
    };

    private void VerifyNotSaved() =>
        _factory.Store.Verify(s => s.SaveAsync(It.IsAny<RegistrationViewModel>(), It.IsAny<CancellationToken>()), Times.Never);

    // Форма регистрации открывается и содержит токен.
    [Fact]
    public async Task GetForm_RendersRegistrationForm()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/Form");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"regForm\"", html);
        Assert.Contains("__RequestVerificationToken", html);
    }

    // Форма отправляется на POST /Form.
    [Fact]
    public async Task GetForm_PostsToFormRoute()
    {
        using var client = _factory.CreateClient();

        Assert.Contains("action=\"/Form\"", await client.GetStringAsync("/Form"));
    }

    // Успешная регистрация: 302 на главную и сохранение данных.
    [Fact]
    public async Task PostForm_Valid_SavesAndRedirectsHome()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Form", ValidForm());

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.LocationOf());
        _factory.Store.Verify(s => s.SaveAsync(
            It.Is<RegistrationViewModel>(m => m.Email == "anna@example.com" && m.Country == "Украина"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // После регистрации пользователь сразу вошёл.
    [Fact]
    public async Task PostForm_Valid_SignsUserIn()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Form", ValidForm());
        var html = await (await client.GetAsync("/")).ReadHtmlAsync();

        Assert.NotNull(response.GetSetCookie(".AspNetCore.Cookies"));
        Assert.Contains("Привет, Anna!", html);
    }

    // Без любого обязательного поля регистрация не проходит.
    [Theory]
    [InlineData("name")]
    [InlineData("lastName")]
    [InlineData("email")]
    [InlineData("password")]
    [InlineData("confirmPassword")]
    [InlineData("phone")]
    [InlineData("country")]
    [InlineData("agreement")]
    public async Task PostForm_MissingRequiredField_ReturnsForm(string field)
    {
        var form = ValidForm();
        form.Remove(field);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Form", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        VerifyNotSaved();
    }

    // Некорректные значения отдельных полей.
    [Theory]
    [InlineData("country", "USA")]
    [InlineData("birthDate", "2999-01-01")]
    [InlineData("birthDate", "1850-05-05")]
    [InlineData("email", "not-an-email")]
    [InlineData("password", "123")]
    [InlineData("confirmPassword", "different")]
    public async Task PostForm_InvalidValue_ReturnsForm(string field, string value)
    {
        var form = ValidForm();
        form[field] = value;
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Form", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"regForm\"", await response.Content.ReadAsStringAsync());
        VerifyNotSaved();
    }

    // Занятая почта — сообщение на форме.
    [Fact]
    public async Task PostForm_ExistingEmail_ShowsError()
    {
        _factory.Store.Setup(s => s.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Form", ValidForm());

        Assert.Contains("Этот email уже зарегистрирован", await response.ReadHtmlAsync());
        VerifyNotSaved();
    }

    // Сбой хранилища — общее сообщение, без 500.
    [Fact]
    public async Task PostForm_StoreFailure_ShowsGenericError()
    {
        _factory.Store.Setup(s => s.SaveAsync(It.IsAny<RegistrationViewModel>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("down"));
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Form", ValidForm());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Не удалось сохранить данные", await response.ReadHtmlAsync());
    }

    // Отправка без токена — 400.
    [Fact]
    public async Task PostForm_WithoutToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync("/Form", new FormUrlEncodedContent(ValidForm()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        VerifyNotSaved();
    }
}

/// <summary>/Preferences: кулинарная анкета и запись в Firestore.</summary>
public class PreferencesEndpointTests : IClassFixture<MockedAppFactory>
{
    private readonly MockedAppFactory _factory;

    public PreferencesEndpointTests(MockedAppFactory factory)
    {
        _factory = factory;
        _factory.ResetMocks();
    }

    private void EmailRegistered(bool exists) =>
        _factory.Store.Setup(s => s.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(exists);

    private static List<KeyValuePair<string, string>> Form(params (string Key, string Value)[] extra)
    {
        var form = new List<KeyValuePair<string, string>> { new("email", "Anna@Example.com") };
        form.AddRange(extra.Select(e => new KeyValuePair<string, string>(e.Key, e.Value)));
        return form;
    }

    // Анкета открывается и содержит форму.
    [Fact]
    public async Task GetPreferences_RendersForm()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/Preferences");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("id=\"prefForm\"", await response.Content.ReadAsStringAsync());
    }

    // Варианты ответов выводятся на странице.
    [Theory]
    [InlineData("Новичок")]
    [InlineData("Каждый день")]
    [InlineData("Кето")]
    [InlineData("Аэрогриль")]
    public async Task GetPreferences_RendersOptions(string option)
    {
        using var client = _factory.CreateClient();

        Assert.Contains(option, await (await client.GetAsync("/Preferences")).ReadHtmlAsync());
    }

    // Успешная отправка: 302, одна запись в Firestore, затем экран «Спасибо».
    [Fact]
    public async Task PostPreferences_RegisteredEmail_SavesAndShowsThanks()
    {
        EmailRegistered(true);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Preferences",
            Form(("cookingLevel", "Новичок"), ("interests", "Супы"), ("interests", "Напитки")));
        var thanks = await client.GetStringAsync("/Preferences");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Preferences", response.LocationOf());
        var fields = _factory.Firestore.SingleWrite().Update.Fields;
        Assert.Equal("anna@example.com", fields["email"].StringValue);
        Assert.Equal(2, fields["interests"].ArrayValue.Values.Count);
        Assert.Contains("form-success__icon", thanks);
    }

    // Незарегистрированная почта — ошибка, в Firestore ничего не пишется.
    [Fact]
    public async Task PostPreferences_UnknownEmail_ShowsError()
    {
        EmailRegistered(false);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Preferences", Form());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Этот email не найден", await response.ReadHtmlAsync());
        Assert.Empty(_factory.Firestore.Commits);
    }

    // Подделанные значения выпадающих списков отклоняются.
    [Theory]
    [InlineData("cookingLevel", "Шеф")]
    [InlineData("frequency", "Никогда")]
    [InlineData("cookingTime", "Сутки")]
    [InlineData("diet", "Сыроедение")]
    public async Task PostPreferences_ForgedOption_IsRejected(string field, string value)
    {
        EmailRegistered(true);
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Preferences", Form((field, value)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(_factory.Firestore.Commits);
    }

    // Неизвестные интересы отбрасываются перед записью.
    [Fact]
    public async Task PostPreferences_UnknownInterests_AreDropped()
    {
        EmailRegistered(true);
        using var client = _factory.CreateNoRedirectClient();

        await client.PostFormWithTokenAsync("/Preferences", Form(("interests", "Хакерство"), ("interests", "Супы")));

        var interests = _factory.Firestore.SingleWrite().Update.Fields["interests"].ArrayValue.Values;
        Assert.Equal("Супы", Assert.Single(interests).StringValue);
    }

    // Пустая почта — ошибка валидации.
    [Fact]
    public async Task PostPreferences_EmptyEmail_ReturnsForm()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Preferences",
            new List<KeyValuePair<string, string>> { new("email", "") });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        _factory.Store.Verify(s => s.EmailExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Сбой Firestore — общее сообщение, без 500.
    [Fact]
    public async Task PostPreferences_FirestoreFailure_ShowsGenericError()
    {
        EmailRegistered(true);
        _factory.Firestore.FailCommits = true;
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostFormWithTokenAsync("/Preferences", Form());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Не удалось сохранить данные", await response.ReadHtmlAsync());
    }

    // Отправка без токена — 400.
    [Fact]
    public async Task PostPreferences_WithoutToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync("/Preferences", new FormUrlEncodedContent(Form()));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
