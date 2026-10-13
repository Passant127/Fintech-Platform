using FintechPlatform.Api.Data;
using FintechPlatform.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

app.MapPost("/transactions", async (
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
    CreateTransactionRequest request,
    AppDbContext db) =>
{
    // 3. The header is required
    if (string.IsNullOrWhiteSpace(idempotencyKey))
        return Results.BadRequest("The Idempotency-Key header is required.");

    if (idempotencyKey.Length > 100)
        return Results.BadRequest("Idempotency-Key must be 100 characters or less.");

    // 4. Seen this key before? Return the saved response and do nothing else
    var existing = await db.IdempotencyKeys
        .AsNoTracking()
        .FirstOrDefaultAsync(k => k.Key == idempotencyKey);

    if (existing is not null)
        return SavedResponse(existing);

    // Normal validation
    if (request.Amount <= 0)
        return Results.BadRequest("Amount must be greater than zero.");

    if (string.IsNullOrWhiteSpace(request.Currency) || request.Currency.Length != 3)
        return Results.BadRequest("Currency must be a 3-letter code, e.g. USD.");

    // 5. New key: create the transaction AND save the key in ONE database transaction
    await using var dbTransaction = await db.Database.BeginTransactionAsync();

    try
    {
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            AccountId = request.AccountId,
            Amount = request.Amount,
            Currency = request.Currency.ToUpperInvariant(),
            Status = TransactionStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        var responseBody = JsonSerializer.Serialize(transaction, JsonSerializerOptions.Web);

        db.Transactions.Add(transaction);
        db.IdempotencyKeys.Add(new IdempotencyKey
        {
            Key = idempotencyKey,
            ResponseBody = responseBody,
            StatusCode = StatusCodes.Status201Created,
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        await dbTransaction.CommitAsync();

        return Results.Text(responseBody, "application/json", statusCode: StatusCodes.Status201Created);
    }
    catch (DbUpdateException ex) when (IsUniqueKeyViolation(ex))
    {
        // Another request with the same key won the race.
        // Our insert was rejected, so undo everything and return the winner's response.
        await dbTransaction.RollbackAsync();
        db.ChangeTracker.Clear();

        var saved = await db.IdempotencyKeys
            .AsNoTracking()
            .FirstAsync(k => k.Key == idempotencyKey);

        return SavedResponse(saved);
    }
});

static IResult SavedResponse(IdempotencyKey saved) =>
    Results.Text(saved.ResponseBody, "application/json", statusCode: saved.StatusCode);

static bool IsUniqueKeyViolation(DbUpdateException ex) =>
    ex.InnerException is SqlException { Number: 2601 or 2627 };


// GET /transactions/{id} — read one transaction
app.MapGet("/transactions/{id:guid}", async (Guid id, AppDbContext db) =>
{
    var transaction = await db.Transactions.FindAsync(id);
    return transaction is null ? Results.NotFound() : Results.Ok(transaction);
});

app.Run();

// What the client sends when creating a transaction
public record CreateTransactionRequest(Guid AccountId, decimal Amount, string Currency);