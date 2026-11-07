using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Project001.Services;

namespace Project001.IntegrationTests.Startup;

/// <summary>Настоящая DI-конфигурация Program.cs (подменён только FirestoreDb).</summary>
public class StartupTests : IClassFixture<RealServicesFactory>
{
    private readonly RealServicesFactory _factory;

    public StartupTests(RealServicesFactory factory) => _factory = factory;

    // Каждый интерфейс разрешается в свою реализацию.
    [Theory]
    [InlineData(typeof(IRecipeService), typeof(RecipeService))]
    [InlineData(typeof(IBlogService), typeof(BlogService))]
    [InlineData(typeof(IPostService), typeof(PostService))]
    [InlineData(typeof(ICommentService), typeof(CommentService))]
    [InlineData(typeof(IRatingService), typeof(RatingService))]
    [InlineData(typeof(IRecipeRepository), typeof(RecipeRepository))]
    [InlineData(typeof(IBlogRepository), typeof(BlogRepository))]
    [InlineData(typeof(IPostRepository), typeof(PostRepository))]
    [InlineData(typeof(ICommentRepository), typeof(CommentRepository))]
    [InlineData(typeof(IRatingRepository), typeof(RatingRepository))]
    [InlineData(typeof(IRegistrationStore), typeof(RegistrationRepository))]
    public void Service_ResolvesToImplementation(Type service, Type implementation)
    {
        using var scope = _factory.Services.CreateScope();

        Assert.IsType(implementation, scope.ServiceProvider.GetRequiredService(service));
    }

    // Сервисы scoped: один экземпляр на запрос.
    [Fact]
    public void Services_AreScoped()
    {
        using var a = _factory.Services.CreateScope();
        using var b = _factory.Services.CreateScope();

        Assert.Same(a.ServiceProvider.GetRequiredService<IRecipeService>(), a.ServiceProvider.GetRequiredService<IRecipeService>());
        Assert.NotSame(a.ServiceProvider.GetRequiredService<IRecipeService>(), b.ServiceProvider.GetRequiredService<IRecipeService>());
    }

    // Схема по умолчанию — cookie.
    [Fact]
    public async Task DefaultAuthenticationScheme_IsCookies()
    {
        var provider = _factory.Services.GetRequiredService<IAuthenticationSchemeProvider>();

        Assert.Equal(CookieAuthenticationDefaults.AuthenticationScheme, (await provider.GetDefaultAuthenticateSchemeAsync())!.Name);
    }

    // Настройки cookie: адреса входа/выхода, срок 7 дней, скользящее продление.
    [Fact]
    public void CookieOptions_AreConfigured()
    {
        var options = _factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);

        Assert.Equal("/Account/Login", options.LoginPath.Value);
        Assert.Equal("/Account/Logout", options.LogoutPath.Value);
        Assert.Equal(TimeSpan.FromDays(7), options.ExpireTimeSpan);
        Assert.True(options.SlidingExpiration);
    }

    // Все контроллеры приложения зарегистрированы как действия MVC.
    [Theory]
    [InlineData("Home")]
    [InlineData("Recipes")]
    [InlineData("Blogs")]
    [InlineData("Posts")]
    [InlineData("Comments")]
    [InlineData("Ratings")]
    [InlineData("Account")]
    [InlineData("Form")]
    [InlineData("Preferences")]
    [InlineData("Admin")]
    public void Controller_IsDiscovered(string controller)
    {
        var actions = _factory.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items;

        Assert.Contains(actions.OfType<ControllerActionDescriptor>(), a => a.ControllerName == controller);
    }
}
