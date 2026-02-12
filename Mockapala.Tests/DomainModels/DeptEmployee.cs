namespace Mockapala.Tests.DomainModels;

/// <summary>
/// Employee with a department — used for same-department manager tests.
/// </summary>
public class DeptEmployee
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
    public int? ManagerId { get; set; }
}
