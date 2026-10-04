using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using UrlShortener.Api.Infrastructure;
using UrlShortener.Api.Models;
using UrlShortener.Api.Models.Dtos;
using UrlShortener.Api.Repositories;
using UrlShortener.Api.Services;

namespace UrlShortener.Tests.Unit;

public sealed class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock = new();
    private readonly AuthService _sut;

    private static readonly JwtOptions ValidJwtOptions = new()
    {
        Issuer = "TestIssuer",
        Audience = "TestAudience",
        SecretKey = "ThisIsATestSecretKeyThatIsLongEnough!",
        ExpirationHours = 1
    };

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepositoryMock.Object,
            Options.Create(ValidJwtOptions));
    }

    // ---- Register tests ----

    [Fact]
    public async Task RegisterAsync_ValidCredentials_ReturnsAuthResponseWithToken()
    {
        _userRepositoryMock
            .Setup(r => r.ExistsByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) =>
            {
                u.Id = 1;
                return u;
            });

        var request = new RegisterRequest { Email = "test@example.com", Password = "password123" };
        var result = await _sut.RegisterAsync(request);

        result.Should().NotBeNull();
        result.Email.Should().Be("test@example.com");
        result.Token.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task RegisterAsync_PasswordIsHashed_NotStoredInPlainText()
    {
        User? capturedUser = null;
        _userRepositoryMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((u, _) => capturedUser = u)
            .ReturnsAsync((User u, CancellationToken _) => { u.Id = 1; return u; });

        var request = new RegisterRequest { Email = "test@example.com", Password = "mypassword" };
        await _sut.RegisterAsync(request);

        capturedUser.Should().NotBeNull();
        capturedUser!.PasswordHash.Should().NotBe("mypassword");
        BCrypt.Net.BCrypt.Verify("mypassword", capturedUser.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        _userRepositoryMock
            .Setup(r => r.ExistsByEmailAsync("existing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new RegisterRequest { Email = "existing@example.com", Password = "password123" };
        Func<Task> act = () => _sut.RegisterAsync(request);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task RegisterAsync_EmptyEmail_ThrowsArgumentException()
    {
        var request = new RegisterRequest { Email = "", Password = "password123" };
        Func<Task> act = () => _sut.RegisterAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("12345")]
    public async Task RegisterAsync_ShortPassword_ThrowsArgumentException(string password)
    {
        var request = new RegisterRequest { Email = "test@example.com", Password = password };
        Func<Task> act = () => _sut.RegisterAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*6 characters*");
    }

    // ---- Login tests ----

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsAuthResponseWithToken()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword");
        var existingUser = new User
        {
            Id = 42,
            Email = "user@example.com",
            PasswordHash = passwordHash
        };

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var request = new LoginRequest { Email = "user@example.com", Password = "correctpassword" };
        var result = await _sut.LoginAsync(request);

        result.Should().NotBeNull();
        result.Email.Should().Be("user@example.com");
        result.Token.Should().NotBeNullOrEmpty();
        result.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsUnauthorizedAccessException()
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("correctpassword");
        var existingUser = new User
        {
            Id = 1,
            Email = "user@example.com",
            PasswordHash = passwordHash
        };

        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingUser);

        var request = new LoginRequest { Email = "user@example.com", Password = "wrongpassword" };
        Func<Task> act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid*");
    }

    [Fact]
    public async Task LoginAsync_NonExistentEmail_ThrowsUnauthorizedAccessException()
    {
        _userRepositoryMock
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var request = new LoginRequest { Email = "nobody@example.com", Password = "password123" };
        Func<Task> act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid*");
    }

    [Fact]
    public async Task LoginAsync_EmptyCredentials_ThrowsArgumentException()
    {
        var request = new LoginRequest { Email = "", Password = "" };
        Func<Task> act = () => _sut.LoginAsync(request);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task RegisterAsync_GeneratedToken_ContainsEmailClaim()
    {
        _userRepositoryMock
            .Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepositoryMock
            .Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => { u.Id = 5; return u; });

        var request = new RegisterRequest { Email = "claims@example.com", Password = "securepass" };
        var result = await _sut.RegisterAsync(request);

        // Decode token (no validation, just parse claims)
        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(result.Token);
        jwt.Claims.Should().Contain(c => c.Type == "email" && c.Value == "claims@example.com");
    }
}
