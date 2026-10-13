namespace FintechPlatform.Api.Models;

public class IdempotencyKey
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string ResponseBody { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}