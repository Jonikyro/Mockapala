namespace Mockapala.Tests.DomainModels;

/// <summary>
/// A driver who must be insured by a policy that covers their vehicle class and age group.
/// </summary>
public class InsuredDriver
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VehicleClass { get; set; } = string.Empty; // "Car", "Truck", "Motorcycle"
    public int Age { get; set; }
    public int PolicyId { get; set; }
}
