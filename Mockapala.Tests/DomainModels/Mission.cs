namespace Mockapala.Tests.DomainModels;

/// <summary>
/// A mission with a required clearance level.
/// </summary>
public class Mission
{
    public int Id { get; set; }
    public string CodeName { get; set; } = string.Empty;
    public int RequiredClearance { get; set; }
}
