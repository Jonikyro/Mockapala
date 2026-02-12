namespace Mockapala.Tests.DomainModels;

public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CompanyId { get; set; }
    public bool IsActive { get; set; } = true;
}
