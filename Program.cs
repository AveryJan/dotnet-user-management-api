using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddProblemDetails();
builder.Services.AddSingleton<UserStore>();
var app = builder.Build();

// Centralized exception handling; do not disclose internal exceptions to callers.
app.UseExceptionHandler();

// Custom request/response logging middleware.
app.Use(async (context, next) =>
{
    var timer = Stopwatch.StartNew();
    try
    {
        await next(context);
    }
    finally
    {
        timer.Stop();
        app.Logger.LogInformation("{Method} {Path} => {StatusCode} in {ElapsedMs} ms",
            context.Request.Method, context.Request.Path, context.Response.StatusCode,
            timer.ElapsedMilliseconds);
    }
});

app.MapGet("/", () => Results.Ok(new { service = "User Management API", status = "ready" }));

var users = app.MapGroup("/api/users");

users.MapGet("/", (UserStore store) =>
    Results.Ok(store.All()));

users.MapGet("/{id:guid}", (Guid id, UserStore store) =>
    store.Find(id) is { } user ? Results.Ok(user) : Results.NotFound(new { error = "User not found." }));

users.MapPost("/", (UserRequest request, UserStore store) =>
{
    var errors = Validate(request);
    if (errors.Count != 0) return Results.ValidationProblem(errors);

    if (store.EmailExists(request.Email))
        return Results.Conflict(new { error = "Email already exists." });

    var user = new User(Guid.NewGuid(), request.Name.Trim(), request.Email.Trim().ToLowerInvariant());
    if (!store.Add(user)) return Results.Conflict(new { error = "Email already exists." });
    return Results.Created($"/api/users/{user.Id}", user);
});

users.MapPut("/{id:guid}", (Guid id, UserRequest request, UserStore store) =>
{
    var errors = Validate(request);
    if (errors.Count != 0) return Results.ValidationProblem(errors);

    return store.Update(id, request.Name.Trim(), request.Email.Trim().ToLowerInvariant()) switch
    {
        UpdateResult.NotFound => Results.NotFound(new { error = "User not found." }),
        UpdateResult.DuplicateEmail => Results.Conflict(new { error = "Email already exists." }),
        _ => Results.Ok(store.Find(id))
    };
});

users.MapDelete("/{id:guid}", (Guid id, UserStore store) =>
    store.Delete(id) ? Results.NoContent() : Results.NotFound(new { error = "User not found." }));

app.Run();

static Dictionary<string, string[]> Validate(UserRequest input)
{
    var errors = new List<ValidationResult>();
    Validator.TryValidateObject(input, new ValidationContext(input), errors, true);
    // RequiredAttribute rejects null/empty/whitespace-only values.
    return errors
        .SelectMany(e => (e.MemberNames.Any() ? e.MemberNames : new[] { "request" })
            .Select(member => (member, message: e.ErrorMessage ?? "Invalid value.")))
        .GroupBy(e => e.member)
        .ToDictionary(g => g.Key, g => g.Select(e => e.message).ToArray());
}

public sealed record User(Guid Id, string Name, string Email);

public sealed class UserRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [StringLength(254)]
    public string Email { get; init; } = string.Empty;
}

public enum UpdateResult { Updated, NotFound, DuplicateEmail }

// Thread-safe in-memory storage; data resets when the app restarts.
public sealed class UserStore
{
    private readonly Dictionary<Guid, User> _users = new();
    private readonly object _gate = new();

    public User[] All()
    {
        lock (_gate) return _users.Values.OrderBy(u => u.Name).ToArray();
    }

    public User? Find(Guid id)
    {
        lock (_gate) return _users.GetValueOrDefault(id);
    }

    public bool EmailExists(string email)
    {
        lock (_gate) return _users.Values.Any(u =>
            string.Equals(u.Email, email.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public bool Add(User user)
    {
        lock (_gate)
        {
            if (_users.Values.Any(u => string.Equals(u.Email, user.Email, StringComparison.OrdinalIgnoreCase)))
                return false;
            _users.Add(user.Id, user);
            return true;
        }
    }

    public UpdateResult Update(Guid id, string name, string email)
    {
        lock (_gate)
        {
            if (!_users.ContainsKey(id)) return UpdateResult.NotFound;
            if (_users.Values.Any(u => u.Id != id &&
                string.Equals(u.Email, email, StringComparison.OrdinalIgnoreCase)))
                return UpdateResult.DuplicateEmail;
            _users[id] = new User(id, name, email);
            return UpdateResult.Updated;
        }
    }

    public bool Delete(Guid id)
    {
        lock (_gate) return _users.Remove(id);
    }
}

// Exposed for integration tests using WebApplicationFactory, if added later.
public partial class Program { }
