using QueueLess.Api.DTOs;
using QueueLess.Api.Models;

namespace QueueLess.Api.Services;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);
    Task<UserProfileDto> GetCurrentUserProfileAsync(Guid userId);
    string HashPassword(string password);
    bool VerifyPassword(string password, string hash);
    string GenerateJwtToken(User user, Guid? hospitalId = null);
}
