namespace Mockapala.Tests.DomainModels;

/// <summary>
/// An agent with a clearance level that restricts which missions they can be assigned to.
/// </summary>
public class Agent
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int ClearanceLevel { get; set; }
    public int MissionId { get; set; }
}
