namespace Mockapala.Tests.DomainModels;

public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public decimal Amount { get; set; }
}
