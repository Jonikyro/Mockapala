using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests custom key generators, especially with GUID keys.
/// </summary>
public class GuidKeyTests
{
    [Fact]
    public void GuidKey_Default_GeneratesValidGuids()
    {
        var schema = SchemaCreate.Create()
            .Entity<GuidEntity>(e => e.Key(g => g.Id))
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<GuidEntity>(5));

        var entities = data.Get<GuidEntity>();
        Assert.Equal(5, entities.Count);
        Assert.All(entities, e => Assert.NotEqual(Guid.Empty, e.Id));
        // All GUIDs should be unique
        Assert.Equal(entities.Count, entities.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void GuidKey_CustomGenerator_Works()
    {
        var schema = SchemaCreate.Create()
            .Entity<GuidEntity>(e =>
            {
                e.Key(g => g.Id);
                e.KeyGenerator(KeyGenerators.NewGuid);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<GuidEntity>(10));

        var entities = data.Get<GuidEntity>();
        Assert.Equal(10, entities.Count);
        Assert.All(entities, e => Assert.NotEqual(Guid.Empty, e.Id));
        Assert.Equal(entities.Count, entities.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public void GuidKey_DeterministicSeeded()
    {
        // Using a custom generator that derives GUID from seed + index for determinism
        var schema = SchemaCreate.Create()
            .Entity<GuidEntity>(e =>
            {
                e.Key(g => g.Id);
                e.KeyGenerator<Guid>(i => new Guid(i, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
            })
            .Build();

        var gen = new DataGenerator();
        var data1 = gen.Generate(schema, cfg => cfg.Count<GuidEntity>(5).Seed(42));
        var data2 = gen.Generate(schema, cfg => cfg.Count<GuidEntity>(5).Seed(42));

        var ids1 = data1.Get<GuidEntity>().Select(e => e.Id).ToList();
        var ids2 = data2.Get<GuidEntity>().Select(e => e.Id).ToList();
        Assert.Equal(ids1, ids2);
    }

    [Fact]
    public void StringFormatKey_Works()
    {
        var schema = SchemaCreate.Create()
            .Entity<StringKeyEntity>(e =>
            {
                e.Key(s => s.Code);
                e.KeyGenerator(KeyGenerators.StringFormat("ORD-{0:D4}"));
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<StringKeyEntity>(3));

        var entities = data.Get<StringKeyEntity>();
        Assert.Equal("ORD-0001", entities[0].Code);
        Assert.Equal("ORD-0002", entities[1].Code);
        Assert.Equal("ORD-0003", entities[2].Code);
    }
}

public class StringKeyEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
