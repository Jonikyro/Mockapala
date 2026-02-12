namespace Mockapala.Tests.DomainModels;

public class Address
{
    public int Id { get; set; }
    public string Street { get; set; } = string.Empty;
    public int CountryId { get; set; }
}
