using Investments.Domain.Entities;
using Investments.Domain.Exceptions;

namespace Investments.Domain.Tests;

public class OwnerTests
{
    [Fact]
    public void Create_trims_and_normalizes_the_data()
    {
        var owner = Owner.Create("  Ana Souza ", " Ana@Example.com ");

        Assert.NotEqual(Guid.Empty, owner.Id);
        Assert.Equal("Ana Souza", owner.Name);
        Assert.Equal("ana@example.com", owner.Email);
    }

    [Theory]
    [InlineData("", "ana@example.com")]
    [InlineData("   ", "ana@example.com")]
    [InlineData("Ana", "")]
    [InlineData("Ana", "not-an-email")]
    public void Create_rejects_invalid_data(string name, string email)
    {
        Assert.Throws<DomainException>(() => Owner.Create(name, email));
    }
}
