var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

// Health check endpoint — lightweight, no auth, no DB hit.
// Useful for container orchestration and uptime monitors.
app.MapGet("/health", () => Results.Ok(new HealthResponse("Dentara", "0.1.0", "Development")));

app.Run();

internal sealed record HealthResponse(string Service, string Version, string Environment);
