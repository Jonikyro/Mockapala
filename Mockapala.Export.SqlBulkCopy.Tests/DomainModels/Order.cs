namespace Mockapala.Export.SqlBulkCopy.Tests.DomainModels;

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
}
