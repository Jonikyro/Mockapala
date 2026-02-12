namespace Mockapala.Tests.DomainModels;

/// <summary>
/// A software feature that requires a minimum subscription tier.
/// </summary>
public class Feature
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinTier { get; set; } // 1 = Basic, 2 = Pro, 3 = Enterprise
}
