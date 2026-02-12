namespace Mockapala.Tests.DomainModels;

/// <summary>
/// Used for Unique relation tests: each seat is assigned to exactly one person.
/// </summary>
public class Seat
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
}
