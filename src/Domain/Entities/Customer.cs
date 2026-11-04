namespace CoffeeShop.Domain.Entities;

public class Customer
{
    private Customer() { }

    public Customer(int id, string name, string email, string identityUserId)
    {
        Id = id;
        Name = name;
        Email = email;
        IdentityUserId = identityUserId;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string IdentityUserId { get; private set; } = null!;
}
