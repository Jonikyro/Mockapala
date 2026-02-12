using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests for IdealCount (flexible entity counts) vs Count (strict).
/// </summary>
public class IdealCountTests
{
    /// <summary>
    /// IdealCount happy path: Where predicate filters some targets, survivors are between min and count.
    /// Departments have Budget "High" or "Low"; TaskItems only link to "High" budget projects.
    /// Some TaskItems will be discarded because they can't find a "High" project.
    /// </summary>
    [Fact]
    public void IdealCount_HappyPath_SurvivorsWithinRange()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f
                    .RuleFor(c => c.Name, (f, c) => f.IndexFaker % 2 == 0 ? "Active" : "Inactive"));
            })
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId)
                    .Where((customer, company) => company.Name == "Active");
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Company>(10)
            .IdealCount<Customer>(100, min: 20)
            .Seed(42));

        var customers = data.Get<Customer>();
        // Some customers were discarded because they couldn't find an "Active" company
        // (all customers compete for the 5 active companies, but with Where predicate
        // filtering, some might fail). Actually with Random strategy all should succeed
        // since there are active companies. Let's verify count is within range.
        Assert.InRange(customers.Count, 20, 100);

        // All surviving customers point to Active companies
        var companyById = data.Get<Company>().ToDictionary(c => c.Id);
        Assert.All(customers, c =>
        {
            var company = companyById[c.CompanyId];
            Assert.Equal("Active", company.Name);
        });
    }

    /// <summary>
    /// IdealCount where all entities survive because no predicates filter anything.
    /// </summary>
    [Fact]
    public void IdealCount_AllSurvive_WhenNoPredicateFilters()
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
            .Count<Company>(5)
            .IdealCount<Customer>(10, min: 5)
            .Seed(42));

        // No filtering, all 10 should survive
        Assert.Equal(10, data.Get<Customer>().Count);
    }

    /// <summary>
    /// IdealCount throws when survivors fall below minimum.
    /// </summary>
    [Fact]
    public void IdealCount_BelowMinimum_Throws()
    {
        // All companies are "Inactive", so all customers will be discarded
        var companies = new List<Company>
        {
            new() { Id = 1, Name = "Inactive" },
            new() { Id = 2, Name = "Inactive" },
            new() { Id = 3, Name = "Inactive" },
        };

        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId)
                    .Where((customer, company) => company.Name == "Active");
            })
            .Build();

        var gen = new DataGenerator();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(schema, cfg => cfg
                .Prefill(companies)
                .IdealCount<Customer>(50, min: 10)
                .Seed(42)));

        Assert.Contains("0 survived", ex.Message);
        Assert.Contains("Minimum required: 10", ex.Message);
    }

    /// <summary>
    /// IdealCount with min: 0 does not throw even when all entities are discarded.
    /// </summary>
    [Fact]
    public void IdealCount_MinZero_AllDiscarded_NoThrow()
    {
        var companies = new List<Company>
        {
            new() { Id = 1, Name = "Inactive" },
        };

        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId)
                    .Where((customer, company) => company.Name == "Active");
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(companies)
            .IdealCount<Customer>(20, min: 0)
            .Seed(42));

        Assert.Empty(data.Get<Customer>());
    }

    /// <summary>
    /// IdealCount default min is 1: if all entities are discarded, throws because at least 1 is required.
    /// </summary>
    [Fact]
    public void IdealCount_DefaultMin_ThrowsWhenAllDiscarded()
    {
        var companies = new List<Company>
        {
            new() { Id = 1, Name = "Inactive" },
        };

        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId)
                    .Where((customer, company) => company.Name == "Active");
            })
            .Build();

        var gen = new DataGenerator();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(schema, cfg => cfg
                .Prefill(companies)
                .IdealCount<Customer>(10) // default min: 1
                .Seed(42)));

        Assert.Contains("0 survived", ex.Message);
        Assert.Contains("Minimum required: 1", ex.Message);
    }

    /// <summary>
    /// Count (strict) still throws on failure — unchanged behavior.
    /// </summary>
    [Fact]
    public void Count_StillThrows_WhenPredicateExcludesAll()
    {
        var companies = new List<Company>
        {
            new() { Id = 1, Name = "Inactive" },
        };

        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId)
                    .Where((customer, company) => company.Name == "Active");
            })
            .Build();

        var gen = new DataGenerator();
        // Count (strict) — should throw with the original error message, not the IdealCount message
        var ex = Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(schema, cfg => cfg
                .Prefill(companies)
                .Count<Customer>(10)
                .Seed(42)));

        Assert.Contains("excluded all", ex.Message);
    }

    /// <summary>
    /// IdealCount with IsUnique: 10 sources, 5 targets, flexible count allows 5 to link and discards 5.
    /// </summary>
    [Fact]
    public void IdealCount_WithUnique_DiscardsExcessSources()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId).IsUnique();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Company>(5)
            .IdealCount<Customer>(10, min: 3)
            .Seed(42));

        var customers = data.Get<Customer>();
        // Only 5 companies, unique relation, so at most 5 customers survive
        Assert.Equal(5, customers.Count);

        // All FKs point to distinct companies
        var companyIds = customers.Select(c => c.CompanyId).ToHashSet();
        Assert.Equal(5, companyIds.Count);
    }

    /// <summary>
    /// Same seed produces same survivor count with IdealCount.
    /// </summary>
    [Fact]
    public void IdealCount_Determinism_SameSeedSameResult()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f
                    .RuleFor(c => c.Name, (f, c) => f.IndexFaker % 3 == 0 ? "Active" : "Inactive"));
            })
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId)
                    .Where((customer, company) => company.Name == "Active");
            })
            .Build();

        var gen = new DataGenerator();

        var data1 = gen.Generate(schema, cfg => cfg
            .Count<Company>(20)
            .IdealCount<Customer>(50, min: 0)
            .Seed(123));

        var data2 = gen.Generate(schema, cfg => cfg
            .Count<Company>(20)
            .IdealCount<Customer>(50, min: 0)
            .Seed(123));

        Assert.Equal(data1.Get<Customer>().Count, data2.Get<Customer>().Count);

        // Same IDs in same order
        var ids1 = data1.Get<Customer>().Select(c => c.Id).ToList();
        var ids2 = data2.Get<Customer>().Select(c => c.Id).ToList();
        Assert.Equal(ids1, ids2);
    }

    /// <summary>
    /// Validation: IdealCount with min > count throws ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void IdealCount_Validation_MinGreaterThanCount_Throws()
    {
        var gen = new DataGenerator();
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            gen.Generate(schema, cfg => cfg.IdealCount<Company>(5, min: 10)));
    }

    /// <summary>
    /// Validation: IdealCount with count 0 throws ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void IdealCount_Validation_CountZero_Throws()
    {
        var gen = new DataGenerator();
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            gen.Generate(schema, cfg => cfg.IdealCount<Company>(0)));
    }
}
