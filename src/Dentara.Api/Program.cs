using Dentara.Api.Extensions;
using Dentara.Api.Middleware;
using Dentara.Application;
using Dentara.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ─── Services ─────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApi(builder.Configuration);

// OpenAPI / Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ─── Pipeline (order matters) ─────────────────────────────────────────────

// 1. Correlation id — must run first so all downstream logs have it
app.UseMiddleware<CorrelationIdMiddleware>();

// 2. Global exception handler — converts exceptions to JSON error responses
app.UseMiddleware<GlobalExceptionMiddleware>();

// 3. Swagger (Development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 4. CORS
app.UseCors();

// 5. HTTPS redirection
app.UseHttpsRedirection();

// 6. Authentication → Authorization
app.UseAuthentication();
app.UseAuthorization();

// 7. Controllers
app.MapControllers();

// 8. Health check (no auth, no DB — for uptime monitors)
app.MapGet("/health", () => Results.Ok(new
{
    service = "Dentara",
    version = "0.1.0",
    environment = app.Environment.EnvironmentName,
    timestamp = DateTimeOffset.UtcNow
})).AllowAnonymous().WithTags("Health");

app.Run();
