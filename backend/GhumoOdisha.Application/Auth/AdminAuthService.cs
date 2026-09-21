using GhumoOdisha.Application.Auth.Dtos;
using GhumoOdisha.Application.Common;
using GhumoOdisha.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace GhumoOdisha.Application.Auth;

public class AdminAuthService(
    IGhumoOdishaDbContext db,
    IPinHasher passwordHasher,
    IJwtTokenService jwtTokenService) : IAdminAuthService
{
    public async Task<AdminAuthResponse> LoginAsync(AdminLoginRequest request, CancellationToken cancellationToken = default)
    {
        var admin = await db.AdminUsers
            .FirstOrDefaultAsync(a => a.Username == request.Username, cancellationToken);

        if (admin is null || !passwordHasher.Verify(admin.PasswordHash, request.Password))
        {
            throw new InvalidCredentialsException("Invalid username or password.");
        }

        admin.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var token = jwtTokenService.GenerateAdminToken(admin);
        return new AdminAuthResponse(token, admin.AdminUserId, admin.Username, admin.Role);
    }
}
