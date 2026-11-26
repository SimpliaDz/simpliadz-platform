using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Simplia.BuildingBlocks.Tenancy.Abstractions;
using Simplia.Identity.Infrastructure;
using Simplia.Identity.Infrastructure.Data;
using Simplia.Identity.UseCases.Auth.Abstractions;
using Simplia.Identity.UseCases.Permissions.Abstractions;
using Simplia.Identity.UseCases.Roles.Abstractions;
using Simplia.Identity.UseCases.Users.Abstractions;
using Simplia.Identity.UseCases.Users.Commands.UpdateLanguage;
using Xunit;

namespace Simplia.Identity.Tests;

public sealed class IdentityFunctionalIntegrationTests
{
    private const string TestPassword = "TestPassword123!";

    [Theory]
    [InlineData("en", true)]
    [InlineData("ar", true)]
    [InlineData("fr", true)]
    [InlineData("FR", true)]
    [InlineData("de", false)]
    [InlineData("fr-CA", false)]
    [InlineData("", false)]
    public void Language_preference_accepts_only_supported_codes(string code, bool valid)
    {
        var result = new UpdateLanguageValidator().Validate(new UpdateLanguageCommand(Guid.NewGuid(), code));
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public async Task Each_user_can_change_own_language_without_affecting_other_users()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IIdentityUserService>();
            var first = await users.CreateUserAsync("language1@test.local", "First", "User",
                TestPassword, null, CancellationToken.None);
            var second = await users.CreateUserAsync("language2@test.local", "Second", "User",
                TestPassword, null, CancellationToken.None);

            Assert.Equal("en", (await users.GetUserByIdAsync(first, CancellationToken.None))!.LanguageCode);
            await users.UpdateLanguageAsync(first, "ar", CancellationToken.None);

            await using var nextScope = provider.CreateAsyncScope();
            var nextUsers = nextScope.ServiceProvider.GetRequiredService<IIdentityUserService>();
            Assert.Equal("ar", (await nextUsers.GetUserByIdAsync(first, CancellationToken.None))!.LanguageCode);
            Assert.Equal("en", (await nextUsers.GetUserByIdAsync(second, CancellationToken.None))!.LanguageCode);
        });
    }

    [Fact]
    public async Task Roles_users_and_permissions_can_be_managed_without_losing_user_history()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var roles = scope.ServiceProvider.GetRequiredService<IIdentityRoleService>();
            var users = scope.ServiceProvider.GetRequiredService<IIdentityUserService>();
            var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var permission = (await permissions.GetAllAsync(CancellationToken.None)).First().Name;

            var roleId = await roles.CreateRoleAsync("SalesTester", CancellationToken.None);
            Assert.Contains(await roles.GetRolesAsync(CancellationToken.None), role => role.Id == roleId);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                roles.CreateRoleAsync("SalesTester", CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                roles.AssignPermissionsAsync(roleId, ["unknown.permission"], CancellationToken.None));

            var userId = await users.CreateUserAsync("cashier@test.local", "Test", "Cashier",
                TestPassword, ["SalesTester"], CancellationToken.None);
            Assert.Contains(await users.GetUsersAsync(CancellationToken.None), user => user.Id == userId);
            Assert.Contains("SalesTester", (await users.GetUserByIdAsync(userId, CancellationToken.None))!.Roles);
            Assert.Empty(await users.GetEffectivePermissionsAsync(userId, CancellationToken.None));

            await roles.AssignPermissionsAsync(roleId, [permission], CancellationToken.None);
            Assert.Contains(permission, (await roles.GetRoleByIdAsync(roleId, CancellationToken.None))!.Permissions);
            Assert.Contains(permission, await users.GetEffectivePermissionsAsync(userId, CancellationToken.None));
            await roles.RemovePermissionsAsync(roleId, [permission], CancellationToken.None);
            Assert.DoesNotContain(permission, await users.GetEffectivePermissionsAsync(userId, CancellationToken.None));

            await users.RemoveRolesAsync(userId, ["SalesTester"], CancellationToken.None);
            Assert.Empty((await users.GetUserByIdAsync(userId, CancellationToken.None))!.Roles);
            await users.AssignRolesAsync(userId, ["SalesTester"], CancellationToken.None);
            await users.DeactivateAsync(userId, CancellationToken.None);
            Assert.False((await users.GetUserByIdAsync(userId, CancellationToken.None))!.IsActive);
            Assert.NotNull(await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId));
            await users.ActivateAsync(userId, CancellationToken.None);
            Assert.True((await users.GetUserByIdAsync(userId, CancellationToken.None))!.IsActive);
        });
    }

    [Fact]
    public async Task Login_refresh_logout_and_deactivation_enforce_session_rules()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IIdentityUserService>();
            var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthService>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var userId = await users.CreateUserAsync("session@test.local", "Test", "Session",
                TestPassword, null, CancellationToken.None);

            Assert.Null(await auth.LoginAsync("session@test.local", "wrong", null, null,
                CancellationToken.None));
            var first = await auth.LoginAsync("session@test.local", TestPassword, null, null,
                CancellationToken.None);
            Assert.NotNull(first);
            Assert.False(string.IsNullOrWhiteSpace(first.AccessToken));
            Assert.Equal(2, await db.LoginAttempts.CountAsync(x => x.UserId == userId));

            var rotated = await auth.RefreshAsync(first.RefreshToken, CancellationToken.None);
            Assert.NotEqual(first.RefreshToken, rotated.RefreshToken);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                auth.RefreshAsync(first.RefreshToken, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                auth.RefreshAsync(rotated.RefreshToken, CancellationToken.None));

            var second = await auth.LoginAsync("session@test.local", TestPassword, null, null,
                CancellationToken.None);
            Assert.NotNull(second);
            await auth.RevokeRefreshTokenAsync(second.RefreshToken, CancellationToken.None);
            await auth.RevokeRefreshTokenAsync(second.RefreshToken, CancellationToken.None);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                auth.RefreshAsync(second.RefreshToken, CancellationToken.None));

            var third = await auth.LoginAsync("session@test.local", TestPassword, null, null,
                CancellationToken.None);
            Assert.NotNull(third);
            await users.DeactivateAsync(userId, CancellationToken.None);
            Assert.Null(await auth.LoginAsync("session@test.local", TestPassword, null, null,
                CancellationToken.None));
            await using var nextRequest = provider.CreateAsyncScope();
            var nextRequestAuth = nextRequest.ServiceProvider.GetRequiredService<IIdentityAuthService>();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                nextRequestAuth.RefreshAsync(third.RefreshToken, CancellationToken.None));
        });
    }

    [Fact]
    public async Task Password_change_and_reset_invalidate_old_credentials_and_sessions()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IIdentityUserService>();
            var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthService>();
            var userId = await users.CreateUserAsync("password@test.local", "Test", "Password",
                TestPassword, null, CancellationToken.None);
            var beforeChange = await auth.LoginAsync("password@test.local", TestPassword,
                null, null, CancellationToken.None);
            Assert.NotNull(beforeChange);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                auth.ChangePasswordAsync(userId, "wrong", "ChangedPassword123!",
                    CancellationToken.None));
            await auth.ChangePasswordAsync(userId, TestPassword, "ChangedPassword123!",
                CancellationToken.None);
            Assert.Null(await auth.LoginAsync("password@test.local", TestPassword,
                null, null, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                auth.RefreshAsync(beforeChange.RefreshToken, CancellationToken.None));

            Assert.Empty((await auth.GeneratePasswordResetTokenAsync("unknown@test.local",
                CancellationToken.None)).ResetToken);
            var resetToken = (await auth.GeneratePasswordResetTokenAsync("password@test.local",
                CancellationToken.None)).ResetToken;
            Assert.False(string.IsNullOrWhiteSpace(resetToken));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                auth.ResetPasswordAsync("password@test.local", "invalid", "ResetPassword123!",
                    CancellationToken.None));
            await auth.ResetPasswordAsync("password@test.local", resetToken, "ResetPassword123!",
                CancellationToken.None);
            Assert.Null(await auth.LoginAsync("password@test.local", "ChangedPassword123!",
                null, null, CancellationToken.None));
            Assert.NotNull(await auth.LoginAsync("password@test.local", "ResetPassword123!",
                null, null, CancellationToken.None));
        });
    }

    [Fact]
    public async Task Role_permission_change_revokes_existing_sessions_and_invalidates_access_stamp()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var roles = scope.ServiceProvider.GetRequiredService<IIdentityRoleService>();
            var users = scope.ServiceProvider.GetRequiredService<IIdentityUserService>();
            var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthService>();
            var validator = scope.ServiceProvider.GetRequiredService<IIdentityAccessTokenValidator>();
            var permissions = scope.ServiceProvider.GetRequiredService<IPermissionService>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var roleId = await roles.CreateRoleAsync("LimitedTester", CancellationToken.None);
            var userId = await users.CreateUserAsync("permissions@test.local", "Test", "Permissions",
                TestPassword, ["LimitedTester"], CancellationToken.None);
            var session = await auth.LoginAsync("permissions@test.local", TestPassword,
                null, null, CancellationToken.None);
            Assert.NotNull(session);
            var stamp = (await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId)).SecurityStamp!;
            Assert.True(await validator.IsValidAsync(userId, stamp, CancellationToken.None));

            var permission = (await permissions.GetAllAsync(CancellationToken.None)).First().Name;
            await roles.AssignPermissionsAsync(roleId, [permission], CancellationToken.None);
            await using var nextRequest = provider.CreateAsyncScope();
            var nextAuth = nextRequest.ServiceProvider.GetRequiredService<IIdentityAuthService>();
            var nextValidator = nextRequest.ServiceProvider
                .GetRequiredService<IIdentityAccessTokenValidator>();
            Assert.False(await nextValidator.IsValidAsync(userId, stamp, CancellationToken.None));
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                nextAuth.RefreshAsync(session.RefreshToken, CancellationToken.None));
            Assert.Contains(permission, await users.GetEffectivePermissionsAsync(userId,
                CancellationToken.None));
        });
    }

    [Fact]
    public async Task Five_bad_password_attempts_lock_the_user_out()
    {
        await InDatabaseAsync(async provider =>
        {
            await using var scope = provider.CreateAsyncScope();
            var users = scope.ServiceProvider.GetRequiredService<IIdentityUserService>();
            var auth = scope.ServiceProvider.GetRequiredService<IIdentityAuthService>();
            var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            var userId = await users.CreateUserAsync("lockout@test.local", "Test", "Lockout",
                TestPassword, null, CancellationToken.None);
            for (var attempt = 0; attempt < 5; attempt++)
                Assert.Null(await auth.LoginAsync("lockout@test.local", "wrong", null, null,
                    CancellationToken.None));
            Assert.Null(await auth.LoginAsync("lockout@test.local", TestPassword, null, null,
                CancellationToken.None));
            Assert.Equal(6, await db.LoginAttempts.CountAsync(x => x.UserId == userId));
            Assert.True((await db.Users.AsNoTracking().SingleAsync(x => x.Id == userId))
                .LockoutEnd > DateTimeOffset.UtcNow);
        });
    }

    private static async Task InDatabaseAsync(Func<IServiceProvider, Task> test)
    {
        var baseConnection = Environment.GetEnvironmentVariable("SIMPLIA_INTEGRATION_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(baseConnection)) return;

        var name = $"SimpliaIdentityTest_{Guid.NewGuid():N}";
        var connection = new SqlConnectionStringBuilder(baseConnection)
            { InitialCatalog = name }.ConnectionString;
        await using (var master = new SqlConnection(new SqlConnectionStringBuilder(baseConnection)
                     { InitialCatalog = "master" }.ConnectionString))
        {
            await master.OpenAsync();
            await using var command = new SqlCommand($"CREATE DATABASE [{name}]", master);
            await command.ExecuteNonQueryAsync();
        }

        try
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:SigningKey"] = "Local-Test-Key-Must-Be-At-Least-32-Characters",
                    ["Jwt:Issuer"] = "Simplia",
                    ["Jwt:Audience"] = "Simplia",
                    ["Identity:ExposePasswordResetToken"] = "true"
                }).Build();
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton<ITenantContext>(new TestTenant(connection));
            var installer = new IdentityModuleInstaller();
            installer.Register(services, connection);
            await using var provider = services.BuildServiceProvider();
            await installer.MigrateAsync(provider, CancellationToken.None);
            await installer.SeedAsync(provider, CancellationToken.None);
            await test(provider);
        }
        finally
        {
            await using var cleanup = new IdentityDbContext(
                new DbContextOptionsBuilder<IdentityDbContext>()
                    .UseSqlServer(connection).Options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestTenant(string connection) : ITenantContext
    {
        public string TenantId => "identity-test";
        public string Name => "identity-test";
        public string ConnectionString => connection;
        public bool IsModuleEnabled(string moduleName) => moduleName == "Identity";
    }
}
