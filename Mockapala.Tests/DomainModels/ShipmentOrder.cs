namespace Mockapala.Tests.DomainModels;

/// <summary>
/// An order that must ship from a warehouse in the same region.
/// </summary>
public class ShipmentOrder
{
    public int Id { get; set; }
    public string Region { get; set; } = string.Empty;
    public int WarehouseId { get; set; }
}
