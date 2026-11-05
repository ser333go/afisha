namespace Afisha.Api.Contracts;

public sealed record RegisterRequest(string Email, string DisplayName, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record UserDto(int Id, string Email, string DisplayName, string Role);

public sealed record AuthResponse(string Token, UserDto User);
