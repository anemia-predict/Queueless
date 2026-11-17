namespace QueueLess.Api.Services;

public class HospitalAiChatRequestDto
{
    public string Message { get; set; } = string.Empty;
}

public class HospitalAiChatResponseDto
{
    public string Reply { get; set; } = string.Empty;
    public string? QueryCategory { get; set; }
    public object? StructuredData { get; set; }
    public List<string> SuggestedFollowUps { get; set; } = new();
}

public interface IHospitalAiChatService
{
    Task<HospitalAiChatResponseDto> ProcessQueryAsync(Guid hospitalId, string userMessage);
}
