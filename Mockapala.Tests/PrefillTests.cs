using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests Prefill: supplying handwritten data instead of generating.
/// </summary>
public class PrefillTests
{
    [Fact]
    public void Prefill_UsesSuppliedInstances()
    {
        var countries = new List<Country>
        {
            new() { Id = 1, Name = "Finland", Code = "FI" },
            new() { Id = 2, Name = "Sweden", Code = "SE" },
            new() { Id = 3, Name = "Norway", Code = "NO" },
        };

        var schema = SchemaCreate.Create()
            .Entity<Country>(e => e.Key(c => c.Id))
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Prefill(countries));

        var result = data.Get<Country>();
        Assert.Equal(3, result.Count);
        Assert.Equal("Finland", result[0].Name);
        Assert.Equal("Sweden", result[1].Name);
        Assert.Equal("Norway", result[2].Name);
    }

    [Fact]
    public void Prefill_OtherEntitiesReferencePrefilled()
    {
        var countries = new List<Country>
        {
            new() { Id = 1, Name = "Finland", Code = "FI" },
            new() { Id = 2, Name = "Sweden", Code = "SE" },
        };

        var schema = SchemaCreate.Create()
            .Entity<Country>(e => e.Key(c => c.Id))
            .Entity<Address>(e =>
            {
                e.Key(a => a.Id);
                e.Relation<Country>(a => a.CountryId);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(countries)
            .Count<Address>(10)
            .Seed(42));

        var addresses = data.Get<Address>();
        var countryIds = countries.Select(c => c.Id).ToHashSet();

        Assert.Equal(10, addresses.Count);
        Assert.All(addresses, a => Assert.Contains(a.CountryId, countryIds));
    }

    [Fact]
    public void Prefill_IgnoresCount()
    {
        var countries = new List<Country>
        {
            new() { Id = 1, Name = "Finland", Code = "FI" },
        };

        var schema = SchemaCreate.Create()
            .Entity<Country>(e => e.Key(c => c.Id))
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(countries)
            .Count<Country>(999)); // Should be ignored

        var result = data.Get<Country>();
        Assert.Single(result);
    }
}
