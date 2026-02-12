namespace Mockapala.Tests.DomainModels;

public class Course
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool IsOpen { get; set; } = true;
}
