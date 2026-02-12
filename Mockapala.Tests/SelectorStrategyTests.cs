using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests selector strategies: Random, RoundRobin, SpreadEvenly, Weighted.
/// </summary>
public class SelectorStrategyTests
{
    [Fact]
    public void Random_IsDefault_DistributesAcrossTargets()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId); // default = Random
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Company>(3)
            .Count<Customer>(30)
            .Seed(42));

        var customers = data.Get<Customer>();
        var grouped = customers.GroupBy(c => c.CompanyId).ToDictionary(g => g.Key, g => g.Count());

        // With Random, all 3 companies should get at least some customers
        Assert.Equal(3, grouped.Count);
    }

    [Fact]
    public void RoundRobin_DistributesEvenly()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId).WithStrategy(SelectorStrategy.RoundRobin);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Company>(3)
            .Count<Customer>(9)
            .Seed(42));

        var customers = data.Get<Customer>();
        var companies = data.Get<Company>();

        // RoundRobin: customer[0] -> company[0], customer[1] -> company[1], customer[2] -> company[2], etc.
        for (var i = 0; i < customers.Count; i++)
        {
            var expectedCompany = companies[i % companies.Count];
            Assert.Equal(expectedCompany.Id, customers[i].CompanyId);
        }
    }

    [Fact]
    public void SpreadEvenly_DistributesEvenly()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId).WithStrategy(SelectorStrategy.SpreadEvenly);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Company>(4)
            .Count<Customer>(12)
            .Seed(42));

        var customers = data.Get<Customer>();
        var grouped = customers.GroupBy(c => c.CompanyId).ToDictionary(g => g.Key, g => g.Count());

        // 12 customers / 4 companies = 3 each
        Assert.Equal(4, grouped.Count);
        Assert.All(grouped.Values, count => Assert.Equal(3, count));
    }

    [Fact]
    public void Weighted_FavorsHigherWeight()
    {
        // Use prefill so prices are known and deterministic
        var products = new List<Product>
        {
            new() { Id = 1, Name = "Cheap", Price = 10m, IsActive = true },
            new() { Id = 2, Name = "Mid", Price = 100m, IsActive = true },
            new() { Id = 3, Name = "Expensive", Price = 1000m, IsActive = true },
        };

        var schema = SchemaCreate.Create()
            .Entity<Product>(e => e.Key(p => p.Id))
            .Entity<OrderLine>(e =>
            {
                e.Key(ol => ol.Id);
                e.Relation<Product>(ol => ol.ProductId)
                    .WithWeightedStrategy(p => (double)p.Price); // higher price = higher weight
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(products)
            .Count<OrderLine>(3000)
            .Seed(42));

        var orderLines = data.Get<OrderLine>();
        var grouped = orderLines.GroupBy(ol => ol.ProductId).ToDictionary(g => g.Key, g => g.Count());

        // Product 3 (weight 1000) should be picked much more often than product 1 (weight 10)
        Assert.True(grouped[3] > grouped[1],
            $"Expected product 3 (weight 1000) to be picked more than product 1 (weight 10), but got {grouped[3]} vs {grouped[1]}");
    }

    [Fact]
    public void RoundRobin_WithSeed_IsDeterministic()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId).WithStrategy(SelectorStrategy.RoundRobin);
            })
            .Build();

        var gen = new DataGenerator();
        var data1 = gen.Generate(schema, cfg => cfg.Count<Company>(3).Count<Customer>(9).Seed(42));
        var data2 = gen.Generate(schema, cfg => cfg.Count<Company>(3).Count<Customer>(9).Seed(42));

        var ids1 = data1.Get<Customer>().Select(c => c.CompanyId).ToList();
        var ids2 = data2.Get<Customer>().Select(c => c.CompanyId).ToList();
        Assert.Equal(ids1, ids2);
    }

    [Fact]
    public void Weighted_WithSeed_IsDeterministic()
    {
        var schema = SchemaCreate.Create()
            .Entity<Product>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f.RuleFor(p => p.Price, (f, p) => p.Id * 100m));
            })
            .Entity<OrderLine>(e =>
            {
                e.Key(ol => ol.Id);
                e.Relation<Product>(ol => ol.ProductId)
                    .WithWeightedStrategy(p => (double)p.Price);
            })
            .Build();

        var gen = new DataGenerator();
        var data1 = gen.Generate(schema, cfg => cfg.Count<Product>(5).Count<OrderLine>(100).Seed(42));
        var data2 = gen.Generate(schema, cfg => cfg.Count<Product>(5).Count<OrderLine>(100).Seed(42));

        var ids1 = data1.Get<OrderLine>().Select(ol => ol.ProductId).ToList();
        var ids2 = data2.Get<OrderLine>().Select(ol => ol.ProductId).ToList();
        Assert.Equal(ids1, ids2);
    }
}
