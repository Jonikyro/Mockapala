using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests self-referential relations (e.g. Employee → Manager).
/// </summary>
public class SelfReferenceTests
{
    [Fact]
    public void SelfRef_DoesNotCauseCycleError()
    {
        var schema = SchemaCreate.Create()
            .Entity<Employee>(e =>
            {
                e.Key(emp => emp.Id);
                e.Relation<Employee>(emp => emp.ManagerId).Optional();
            })
            .Build();

        // Should not throw — self-edges are excluded from cycle detection
        Assert.Single(schema.GenerationOrder);
        Assert.Equal(typeof(Employee), schema.GenerationOrder[0]);
    }

    [Fact]
    public void SelfRef_ResolvesFromCurrentBatch()
    {
        var schema = SchemaCreate.Create()
            .Entity<Employee>(e =>
            {
                e.Key(emp => emp.Id);
                e.WithRules(f => f.RuleFor(emp => emp.Name, f => f.Name.FirstName()));
                e.Relation<Employee>(emp => emp.ManagerId).Optional();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<Employee>(10).Seed(42));

        var employees = data.Get<Employee>();
        Assert.Equal(10, employees.Count);

        var employeeIds = employees.Select(emp => emp.Id).ToHashSet();
        // ManagerId should be null or point to another employee in the batch
        foreach (var emp in employees)
        {
            if (emp.ManagerId.HasValue)
                Assert.Contains(emp.ManagerId.Value, employeeIds);
        }
    }

    [Fact]
    public void SelfRef_WithRule_EmployeeNotOwnManager()
    {
        var schema = SchemaCreate.Create()
            .Entity<Employee>(e =>
            {
                e.Key(emp => emp.Id);
                e.WithRules(f => f.RuleFor(emp => emp.Name, f => f.Name.FirstName()));
                e.Relation<Employee>(emp => emp.ManagerId)
                    .Where((emp, manager) => emp.Id != manager.Id)
                    .Optional();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<Employee>(20).Seed(42));

        var employees = data.Get<Employee>();
        foreach (var emp in employees)
        {
            if (emp.ManagerId.HasValue)
                Assert.NotEqual(emp.Id, emp.ManagerId.Value);
        }
    }
}
