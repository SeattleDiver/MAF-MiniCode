using CustomerPortal.Core;
using Xunit;

namespace CustomerPortal.Tests;

public sealed class CustomerServiceTests
{
    [Fact]
    public void Create_StoresTheCustomer()
    {
        var service = new CustomerService();

        Customer customer = service.Create("Ada", "ada@example.com");

        Assert.Same(customer, Assert.Single(service.Customers));
    }

    [Fact]
    public void Create_TrimsTheName()
    {
        var service = new CustomerService();

        Customer customer = service.Create("  Ada  ", "ada@example.com");

        Assert.Equal("Ada", customer.Name);
    }
}
