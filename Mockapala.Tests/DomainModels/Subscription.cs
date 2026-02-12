namespace Mockapala.Tests.DomainModels;

/// <summary>
/// A user subscription with a tier that gates which features are accessible.
/// </summary>
public class Subscription
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public int Tier { get; set; } // 1 = Basic, 2 = Pro, 3 = Enterprise
    public int FeatureId { get; set; }
}
