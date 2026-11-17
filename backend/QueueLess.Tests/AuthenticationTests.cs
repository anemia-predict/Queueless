using FluentAssertions;
using Microsoft.Extensions.Configuration;
using QueueLess.Api.DTOs;
using QueueLess.Api.Models;
using QueueLess.Api.Services;

namespace QueueLess.Tests;

public class AuthenticationTests
{
    private readonly IConfiguration _config;

    public AuthenticationTests()
    {
        var inMemorySettings = new Dictionary<string, string?>
        {
            {"JwtSettings:Secret", "SuperSecretKeyForQueueLessAppSecurity2026!Min32BytesRequired"},
            {"JwtSettings:Issuer", "QueueLessApi"},
            {"JwtSettings:Audience", "QueueLessApp"},
            {"JwtSettings:ExpiryMinutes", "120"}
        };

        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();
    }

    [Fact]
    public async Task RegisterAsync_ValidPatient_CreatesUserAndReturnsToken()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var authService = new AuthService(db, _config);

        var request = new RegisterRequestDto
        {
            Email = "patient.test@example.com",
            Password = "Password@123",
            FullName = "Test Patient",
            PhoneNumber = "+919999999999",
            Role = UserRoles.Patient
        };

        var response = await authService.RegisterAsync(request);

        response.Should().NotBeNull();
        response.Token.Should().NotBeNullOrWhiteSpace();
        response.User.Email.Should().Be("patient.test@example.com");
        response.User.FullName.Should().Be("Test Patient");
        response.User.Role.Should().Be(UserRoles.Patient);
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var authService = new AuthService(db, _config);

        var request = new RegisterRequestDto
        {
            Email = "duplicate@example.com",
            Password = "Password@123",
            FullName = "User One",
            Role = UserRoles.Patient
        };

        await authService.RegisterAsync(request);

        // Attempt registering same email
        var act = async () => await authService.RegisterAsync(request);
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponse()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var authService = new AuthService(db, _config);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "login.test@example.com",
            Password = "SecurePassword@123",
            FullName = "Login User",
            Role = UserRoles.Patient
        });

        var loginResponse = await authService.LoginAsync(new LoginRequestDto
        {
            Email = "login.test@example.com",
            Password = "SecurePassword@123"
        });

        loginResponse.Should().NotBeNull();
        loginResponse.Token.Should().NotBeNullOrWhiteSpace();
        loginResponse.User.Email.Should().Be("login.test@example.com");
    }

    [Fact]
    public async Task LoginAsync_InvalidPassword_ThrowsUnauthorizedAccessException()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var authService = new AuthService(db, _config);

        await authService.RegisterAsync(new RegisterRequestDto
        {
            Email = "wrongpass@example.com",
            Password = "CorrectPassword@123",
            FullName = "User Two",
            Role = UserRoles.Patient
        });

        var act = async () => await authService.LoginAsync(new LoginRequestDto
        {
            Email = "wrongpass@example.com",
            Password = "WrongPassword@123"
        });

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public void PasswordHashing_VerifiesCorrectly()
    {
        using var db = TestDatabaseFixture.CreateInMemoryDbContext();
        var authService = new AuthService(db, _config);

        var password = "MySecurePassword2026!";
        var hash = authService.HashPassword(password);

        hash.Should().NotBe(password);
        authService.VerifyPassword(password, hash).Should().BeTrue();
        authService.VerifyPassword("WrongPassword", hash).Should().BeFalse();
    }
}
