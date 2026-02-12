namespace Mockapala.Tests.DomainModels;

/// <summary>
/// Used for Unique relation tests: each person is assigned to a unique seat.
/// </summary>
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SeatId { get; set; }
}
