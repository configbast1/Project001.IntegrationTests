using System.Collections.Concurrent;
using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Project001.Models;
using Project001.Services;

namespace Project001.IntegrationTests.Infrastructure;

/// <summary>
/// Приложение целиком (маршруты, фильтры, аутентификация, Razor),
/// но все сервисы и хранилище пользователей — Moq-заглушки,
/// а FirestoreDb работает поверх замоканного клиента (без сети).
/// </summary>
public class MockedAppFactory : WebApplicationFactory<Program>
{
    public Mock<IRecipeService> Recipes { get; } = new();
    public Mock<IBlogService> Blogs { get; } = new();
    public Mock<IPostService> Posts { get; } = new();
    public Mock<ICommentService> Comments { get; } = new();
    public Mock<IRatingService> Ratings { get; } = new();
    public Mock<IRegistrationStore> Store { get; } = new();
    public FirestoreFake Firestore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRecipeService>();
            services.AddSingleton(Recipes.Object);
            services.RemoveAll<IBlogService>();
            services.AddSingleton(Blogs.Object);
            services.RemoveAll<IPostService>();
            services.AddSingleton(Posts.Object);
            services.RemoveAll<ICommentService>();
            services.AddSingleton(Comments.Object);
            services.RemoveAll<IRatingService>();
            services.AddSingleton(Ratings.Object);
            services.RemoveAll<IRegistrationStore>();
            services.AddSingleton(Store.Object);
            services.RemoveAll<FirestoreDb>();
            services.AddSingleton(Firestore.Db);
        });
    }

    /// <summary>Сбрасывает настройки заглушек между тестами одного класса.</summary>
    public void ResetMocks()
    {
        Recipes.Reset();
        Blogs.Reset();
        Posts.Reset();
        Comments.Reset();
        Ratings.Reset();
        Store.Reset();
        Firestore.Reset();
    }

    public HttpClient CreateNoRedirectClient()
        => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>Настраивает пользователя, под которым можно войти через /Account/Login.</summary>
    public void SetupUser(string email = TestUser.Email, string password = TestUser.Password, string name = TestUser.Name)
    {
        Store.Setup(s => s.FindByEmailAsync(It.Is<string>(e => e == email), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StoredUser("user-1", name, email, PasswordHasher.Hash(password)));
    }
}

/// <summary>Фабрика, в которой подменён только FirestoreDb: вся остальная DI-конфигурация настоящая.</summary>
public class RealServicesFactory : WebApplicationFactory<Program>
{
    public FirestoreFake Firestore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<FirestoreDb>();
            services.AddSingleton(Firestore.Db);
        });
    }
}

/// <summary>
/// Настоящие сервисы и контроллеры, но репозитории и хранилище пользователей
/// живут в памяти. Позволяет проверять сквозные сценарии без Firestore.
/// </summary>
public class InMemoryAppFactory : WebApplicationFactory<Program>
{
    public InMemoryRecipeRepository Recipes { get; } = new();
    public InMemoryBlogRepository Blogs { get; } = new();
    public InMemoryPostRepository Posts { get; } = new();
    public InMemoryCommentRepository Comments { get; } = new();
    public InMemoryRatingRepository Ratings { get; } = new();
    public InMemoryRegistrationStore Users { get; } = new();
    public FirestoreFake Firestore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRecipeRepository>();
            services.AddSingleton<IRecipeRepository>(Recipes);
            services.RemoveAll<IBlogRepository>();
            services.AddSingleton<IBlogRepository>(Blogs);
            services.RemoveAll<IPostRepository>();
            services.AddSingleton<IPostRepository>(Posts);
            services.RemoveAll<ICommentRepository>();
            services.AddSingleton<ICommentRepository>(Comments);
            services.RemoveAll<IRatingRepository>();
            services.AddSingleton<IRatingRepository>(Ratings);
            services.RemoveAll<IRegistrationStore>();
            services.AddSingleton<IRegistrationStore>(Users);
            services.RemoveAll<FirestoreDb>();
            services.AddSingleton(Firestore.Db);
        });
    }

    public HttpClient CreateNoRedirectClient()
        => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
}

public static class TestUser
{
    public const string Email = "anna@example.com";
    public const string Password = "secret123";
    public const string Name = "Anna";
}

// ---------------------------------------------------------------------------
// In-memory реализации репозиториев
// ---------------------------------------------------------------------------

public abstract class InMemoryRepository<T> where T : class
{
    private readonly ConcurrentDictionary<string, T> _items = new();
    protected abstract string GetId(T item);
    protected abstract void SetId(T item, string id);

    public Task<List<T>> GetAllAsync() => Task.FromResult(_items.Values.ToList());

    public Task<T?> GetByIdAsync(string id) =>
        Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);

    public Task CreateAsync(T item)
    {
        if (string.IsNullOrEmpty(GetId(item)))
        {
            SetId(item, Guid.NewGuid().ToString("N"));
        }
        _items[GetId(item)] = item;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(T item)
    {
        _items[GetId(item)] = item;
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string id)
    {
        _items.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string id) => Task.FromResult(_items.ContainsKey(id));

    protected Task<List<T>> WhereAsync(Func<T, bool> predicate) =>
        Task.FromResult(_items.Values.Where(predicate).ToList());
}

public class InMemoryRecipeRepository : InMemoryRepository<Recipe>, IRecipeRepository
{
    protected override string GetId(Recipe item) => item.RecipeId;
    protected override void SetId(Recipe item, string id) => item.RecipeId = id;
}

public class InMemoryBlogRepository : InMemoryRepository<Blog>, IBlogRepository
{
    protected override string GetId(Blog item) => item.BlogId;
    protected override void SetId(Blog item, string id) => item.BlogId = id;
}

public class InMemoryPostRepository : InMemoryRepository<Post>, IPostRepository
{
    protected override string GetId(Post item) => item.PostId;
    protected override void SetId(Post item, string id) => item.PostId = id;
    public Task<List<Post>> GetByBlogIdAsync(string blogId) => WhereAsync(p => p.BlogId == blogId);
}

public class InMemoryCommentRepository : InMemoryRepository<Comment>, ICommentRepository
{
    protected override string GetId(Comment item) => item.CommentId;
    protected override void SetId(Comment item, string id) => item.CommentId = id;
    public Task<List<Comment>> GetByPostIdAsync(string postId) => WhereAsync(c => c.PostId == postId);
}

public class InMemoryRatingRepository : InMemoryRepository<Rating>, IRatingRepository
{
    protected override string GetId(Rating item) => item.RatingId;
    protected override void SetId(Rating item, string id) => item.RatingId = id;
    public Task<List<Rating>> GetByRecipeIdAsync(string recipeId) => WhereAsync(r => r.RecipeId == recipeId);
}

/// <summary>Хранилище пользователей в памяти с той же нормализацией почты и хешированием, что и настоящее.</summary>
public class InMemoryRegistrationStore : IRegistrationStore
{
    private readonly ConcurrentDictionary<string, StoredUser> _users = new();

    public int Count => _users.Count;

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct)
        => Task.FromResult(_users.ContainsKey(Normalize(email)));

    public Task<string> SaveAsync(RegistrationViewModel model, CancellationToken ct)
    {
        var id = Guid.NewGuid().ToString("N");
        _users[Normalize(model.Email)] = new StoredUser(id, model.Name.Trim(), model.Email.Trim(), PasswordHasher.Hash(model.Password));
        return Task.FromResult(id);
    }

    public Task<StoredUser?> FindByEmailAsync(string email, CancellationToken ct)
        => Task.FromResult(_users.TryGetValue(Normalize(email), out var user) ? user : null);
}
