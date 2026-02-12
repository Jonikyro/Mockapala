namespace Mockapala.Tests.DomainModels;

/// <summary>
/// An insurance policy covering a specific vehicle class with a minimum driver age.
/// </summary>
public class InsurancePolicy
{
    public int Id { get; set; }
    public string CoveredVehicleClass { get; set; } = string.Empty;
    public int MinDriverAge { get; set; }
}
