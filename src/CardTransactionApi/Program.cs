using CardTransactionApi.Data;
using CardTransactionApi.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Database ---
// Uses SQLite for zero-install persistence. The DB file is created automatically.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Data Source=cardtransactions.db";

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

// --- HTTP client for Treasury API ---
builder.Services.AddHttpClient<IExchangeRateService, ExchangeRateService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});

// --- Controllers + Swagger ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});

var app = builder.Build();

// Auto-create / migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// Global exception handler — returns consistent error format, prevents stack trace leakage
app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        context.Response.StatusCode = 500;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            errorCode = "INTERNAL_ERROR",
            error = "An unexpected error occurred. Please try again later."
        });
    });
});

// Swagger UI (enabled in all environments for easy reviewer testing)
app.UseSwagger();
app.UseSwaggerUI();

app.MapControllers();

app.Run();

// Make the implicit Program class public so the test project can reference it
public partial class Program { }
