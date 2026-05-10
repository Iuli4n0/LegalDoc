using IdentityService.Domain.Entities;
using IdentityService.Infrastructure.Persistence;
using IdentityService.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IdentityService.Test.Infrastructure;

public class UserRepositoryAndDbContextTests
{
    [Fact]
    public async Task Given_UserInDatabase_When_GetByIdAsyncIsCalled_Then_ShouldReturnUser()
    {
        await using var dbContext = CreateDbContext();
        var user = User.Create("user@test.com", "hash", "Test User");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var repository = new UserRepository(dbContext);

        var result = await repository.GetByIdAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal(user.Id, result.Id);
    }

    [Fact]
    public async Task Given_MissingUserInDatabase_When_GetByIdAsyncIsCalled_Then_ShouldReturnNull()
    {
        await using var dbContext = CreateDbContext();
        var repository = new UserRepository(dbContext);

        var result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Given_UserInDatabase_When_GetByEmailAsyncIsCalledWithDifferentCase_Then_ShouldReturnUser()
    {
        await using var dbContext = CreateDbContext();
        var user = User.Create("user@test.com", "hash", "Test User");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var repository = new UserRepository(dbContext);

        var result = await repository.GetByEmailAsync("USER@TEST.COM");

        Assert.NotNull(result);
        Assert.Equal(user.Email, result.Email);
    }

    [Fact]
    public async Task Given_NewUser_When_AddAsyncIsCalled_Then_ShouldPersistUser()
    {
        await using var dbContext = CreateDbContext();
        var repository = new UserRepository(dbContext);
        var user = User.Create("new@test.com", "hash", "New User");

        await repository.AddAsync(user);

        var persisted = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
        Assert.NotNull(persisted);
    }

    [Fact]
    public async Task Given_ExistingUser_When_UpdateAsyncIsCalled_Then_ShouldPersistChanges()
    {
        await using var dbContext = CreateDbContext();
        var user = User.Create("user@test.com", "hash", "Test User");
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();

        var repository = new UserRepository(dbContext);
        user.UpdateLastLogin();

        await repository.UpdateAsync(user);

        var persisted = await dbContext.Users.AsNoTracking().FirstAsync(u => u.Id == user.Id);
        Assert.NotNull(persisted.LastLoginAt);
    }

    [Fact]
    public async Task Given_UserInDatabase_When_ExistsAsyncIsCalledWithDifferentCase_Then_ShouldReturnTrue()
    {
        await using var dbContext = CreateDbContext();
        dbContext.Users.Add(User.Create("exists@test.com", "hash", "Exists User"));
        await dbContext.SaveChangesAsync();

        var repository = new UserRepository(dbContext);

        var result = await repository.ExistsAsync("EXISTS@TEST.COM");

        Assert.True(result);
    }

    [Fact]
    public async Task Given_MissingUserInDatabase_When_ExistsAsyncIsCalled_Then_ShouldReturnFalse()
    {
        await using var dbContext = CreateDbContext();
        var repository = new UserRepository(dbContext);

        var result = await repository.ExistsAsync("missing@test.com");

        Assert.False(result);
    }

    [Fact]
    public void Given_DbContextModel_When_OnModelCreatingRuns_Then_ShouldConfigureUserEntity()
    {
        using var dbContext = CreateDbContext();

        var userEntity = dbContext.Model.FindEntityType(typeof(User));

        Assert.NotNull(userEntity);
        Assert.Equal(nameof(User.Id), userEntity.FindPrimaryKey()!.Properties.Single().Name);
        Assert.Equal(256, userEntity.FindProperty(nameof(User.Email))!.GetMaxLength());
        Assert.Equal(500, userEntity.FindProperty(nameof(User.PasswordHash))!.GetMaxLength());
        Assert.Equal(200, userEntity.FindProperty(nameof(User.FullName))!.GetMaxLength());

        var emailIndex = userEntity.GetIndexes().SingleOrDefault(i => i.Properties.Single().Name == nameof(User.Email));
        Assert.NotNull(emailIndex);
        Assert.True(emailIndex!.IsUnique);
    }

    private static IdentityDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new IdentityDbContext(options);
    }
}
