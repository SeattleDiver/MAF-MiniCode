namespace CustomerPortal.Core;

/// <summary>Creates and stores customers.</summary>
public sealed class CustomerService
{
    private readonly List<Customer> _customers = [];

    /// <summary>The customers created so far.</summary>
    public IReadOnlyList<Customer> Customers => _customers;

    /// <summary>Creates a customer and adds it to the store.</summary>
    public Customer Create(string name, string? email)
    {
        var customer = new Customer(name, email);
        _customers.Add(customer);
        return customer;
    }
}
