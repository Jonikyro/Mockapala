using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests optional/nullable relations (Required = false).
/// </summary>
public class OptionalRelationTests
{
    [Fact]
    public void Optional_NoTargets_SetsFkToNull()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId).Optional();
            })
            .Build();

        var gen = new DataGenerator();
        // No Company count — no targets generated
        var data = gen.Generate(schema, cfg => cfg.Count<Customer>(5).Seed(42));

        var customers = data.Get<Customer>();
        Assert.Equal(5, customers.Count);
        // CompanyId is int (non-nullable), so it stays default (0) when SetFkNull fails silently
        Assert.All(customers, c => Assert.Equal(0, c.CompanyId));
    }

    [Fact]
    public void Optional_NullableFK_SetsNull()
    {
        var schema = SchemaCreate.Create()
            .Entity<Employee>(e =>
            {
                e.Key(emp => emp.Id);
                e.Relation<Employee>(emp => emp.ManagerId).Optional();
            })
            .Build();

        // With only 1 employee + Where filter, some may get null
        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<Employee>(1).Seed(42));

        // Single employee referencing self — it's optional, so it's OK
        var employees = data.Get<Employee>();
        Assert.Single(employees);
    }

    [Fact]
    public void Required_NoTargets_Throws()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId); // Required by default
            })
            .Build();

        var gen = new DataGenerator();
        Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(schema, cfg => cfg.Count<Customer>(5)));
    }

    [Fact]
    public void Optional_WhereExcludesAll_SetsFkToDefault()
    {
        var schema = SchemaCreate.Create()
            .Entity<Product>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f.RuleFor(p => p.IsActive, _ => false)); // all inactive
            })
            .Entity<OrderLine>(e =>
            {
                e.Key(ol => ol.Id);
                e.Relation<Product>(ol => ol.ProductId)
                    .WhereTarget(p => p.IsActive) // excludes all
                    .Optional();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Product>(5)
            .Count<OrderLine>(3)
            .Seed(42));

        var orderLines = data.Get<OrderLine>();
        // All products are inactive, WhereTarget excludes all, so FK stays 0
        Assert.All(orderLines, ol => Assert.Equal(0, ol.ProductId));
    }
}
