using FintechPlatform.Api.Data;
using FintechPlatform.Api.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Database")));

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// POST /transactions — create a transaction
app.MapPost("/transactions", async (CreateTransactionRequest request, AppDbContext db) =>
{
    if (request.Amount <= 0)
        return Results.BadRequest("Amount must be greater than zero.");

    if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
        return Results.BadRequest("Currency must be a 3-letter code, e.g. EGP.");

    var transaction = new Transaction
    {
        Id = Guid.NewGuid(),
        AccountId = request.AccountId,
        Amount = request.Amount,
        Currency = request.Currency.ToUpperInvariant(),
        Status = TransactionStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };

    db.Transactions.Add(transaction);
    await db.SaveChangesAsync();

    return Results.Created($"/transactions/{transaction.Id}", transaction);
});

// GET /transactions/{id} — read one transaction
app.MapGet("/transactions/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var transaction = await db.Transactions.FindAsync(id);
    return transaction is null ? Results.NotFound() : Results.Ok(transaction);
});

app.Run();

// What the client sends when creating a transaction
public record CreateTransactionRequest(Guid AccountId, decimal Amount, string Currency);