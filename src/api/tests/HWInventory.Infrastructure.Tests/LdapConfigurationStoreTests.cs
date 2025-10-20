using System;
using System.Threading;
using System.Threading.Tasks;
using HWInventory.Infrastructure.Persistence;
using HWInventory.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HWInventory.Infrastructure.Tests;

public class LdapConfigurationStoreTests
{
    [Fact]
    public async Task SaveAsync_PersistsAllValues()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var dbContext = new AppDbContext(options);
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(x => x.HttpContext!.User.Identity!.Name).Returns("tester");

        var store = new LdapConfigurationStore(dbContext, httpContextAccessor.Object);

        var update = new LdapConfigurationUpdate
        {
            Enabled = true,
            Host = "ldap.example.com",
            Port = 636,
            UseSsl = true,
            BindDn = "cn=svc,dc=example,dc=com",
            Password = "secret",
            UsersBaseDn = "ou=Users,dc=example,dc=com",
            UsersFilter = "(objectClass=person)",
            AttributeMap = new[] { "mail=email", "department=department" }
        };

        await store.SaveAsync(update, CancellationToken.None);

        var model = await store.GetAsync(CancellationToken.None);

        Assert.True(model.Enabled);
        Assert.Equal("ldap.example.com", model.Host);
        Assert.Equal(636, model.Port);
        Assert.True(model.UseSsl);
        Assert.True(model.HasPassword);
        Assert.Equal("cn=svc,dc=example,dc=com", model.BindDn);
        Assert.Equal("ou=Users,dc=example,dc=com", model.UsersBaseDn);
        Assert.Equal("(objectClass=person)", model.UsersFilter);
        Assert.Contains("mail=email", model.AttributeMap);
        Assert.Contains("department=department", model.AttributeMap);

        // Reset password flow should remove stored password
        update.ResetPassword = true;
        update.Password = null;
        await store.SaveAsync(update, CancellationToken.None);

        model = await store.GetAsync(CancellationToken.None);
        Assert.False(model.HasPassword);
    }
}
