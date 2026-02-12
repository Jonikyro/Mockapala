using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests basic schema building, generation order, and FK resolution.
/// </summary>
public class SchemaAndGenerationTests
{
    [Fact]
    public void SimpleSchema_GeneratesEntitiesInOrder()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId);
            })
            .Build();

        Assert.Equal(2, schema.GenerationOrder.Count);
        Assert.Equal(typeof(Company), schema.GenerationOrder[0]);
        Assert.Equal(typeof(Customer), schema.GenerationOrder[1]);
    }

    [Fact]
    public void Generate_ResolvesForeignKeys()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Company>(3)
            .Count<Customer>(10)
            .Seed(42));

        var companies = data.Get<Company>();
        var customers = data.Get<Customer>();
        var companyIds = companies.Select(c => c.Id).ToHashSet();

        Assert.Equal(3, companies.Count);
        Assert.Equal(10, customers.Count);
        Assert.All(customers, c => Assert.Contains(c.CompanyId, companyIds));
    }

    [Fact]
    public void Generate_WithSeed_IsDeterministic()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId);
            })
            .Build();

        var gen = new DataGenerator();
        var data1 = gen.Generate(schema, cfg => cfg.Count<Company>(5).Count<Customer>(20).Seed(123));
        var data2 = gen.Generate(schema, cfg => cfg.Count<Company>(5).Count<Customer>(20).Seed(123));

        var customers1 = data1.Get<Customer>();
        var customers2 = data2.Get<Customer>();

        for (var i = 0; i < customers1.Count; i++)
            Assert.Equal(customers1[i].CompanyId, customers2[i].CompanyId);
    }

    [Fact]
    public void Generate_MultipleRelations_ResolvesAll()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Product>(e => e.Key(p => p.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId);
            })
            .Entity<Order>(e =>
            {
                e.Key(o => o.Id);
                e.Relation<Customer>(o => o.CustomerId);
            })
            .Entity<OrderLine>(e =>
            {
                e.Key(ol => ol.Id);
                e.Relation<Order>(ol => ol.OrderId);
                e.Relation<Product>(ol => ol.ProductId);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Company>(2)
            .Count<Product>(5)
            .Count<Customer>(4)
            .Count<Order>(8)
            .Count<OrderLine>(20)
            .Seed(42));

        var orders = data.Get<Order>();
        var orderLines = data.Get<OrderLine>();
        var orderIds = orders.Select(o => o.Id).ToHashSet();
        var productIds = data.Get<Product>().Select(p => p.Id).ToHashSet();

        Assert.All(orderLines, ol =>
        {
            Assert.Contains(ol.OrderId, orderIds);
            Assert.Contains(ol.ProductId, productIds);
        });
    }

    [Fact]
    public void Generate_WithRules_AppliesBogusRules()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f.RuleFor(c => c.Name, f => f.Company.CompanyName()));
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<Company>(5).Seed(42));
        var companies = data.Get<Company>();

        Assert.All(companies, c => Assert.False(string.IsNullOrEmpty(c.Name)));
    }

    [Fact]
    public void Build_UnregisteredTargetType_Throws()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            SchemaCreate.Create()
                .Entity<Customer>(e =>
                {
                    e.Key(c => c.Id);
                    e.Relation<Company>(c => c.CompanyId); // Company not registered
                })
                .Build());

        Assert.Contains("Company", ex.Message);
        Assert.Contains("not registered", ex.Message);
    }

    [Fact]
    public void Build_DuplicateEntity_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SchemaCreate.Create()
                .Entity<Company>(e => e.Key(c => c.Id))
                .Entity<Company>(e => e.Key(c => c.Id))
                .Build());
    }

    [Fact]
    public void Build_CircularDependency_Throws()
    {
        // A -> B -> A (different types, not self-ref)
        Assert.Throws<CircularDependencyException>(() =>
            SchemaCreate.Create()
                .Entity<CycleA>(e =>
                {
                    e.Key(a => a.Id);
                    e.Relation<CycleB>(a => a.BId);
                })
                .Entity<CycleB>(e =>
                {
                    e.Key(b => b.Id);
                    e.Relation<CycleA>(b => b.AId);
                })
                .Build());
    }

    [Fact]
    public void Generate_NoCountForType_Skips()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema);

        Assert.Throws<KeyNotFoundException>(() => data.Get<Company>());
    }

    [Fact]
    public void Get_ByType_ReturnsEntities()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<Company>(3));

        var companies = data.Get(typeof(Company));
        Assert.Equal(3, companies.Count);
    }

    [Fact]
    public void Generate_KeysAreSequential()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<Company>(5));
        var ids = data.Get<Company>().Select(c => c.Id).ToList();

        Assert.Equal(new[] { 1, 2, 3, 4, 5 }, ids);
    }
}

// Helper types for cycle test
public class CycleA
{
    public int Id { get; set; }
    public int BId { get; set; }
}

public class CycleB
{
    public int Id { get; set; }
    public int AId { get; set; }
}
