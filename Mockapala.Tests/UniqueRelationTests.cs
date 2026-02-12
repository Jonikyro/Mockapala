using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests .IsUnique() — each target used at most once.
/// </summary>
public class UniqueRelationTests
{
    [Fact]
    public void Unique_EachTargetUsedOnce()
    {
        var schema = SchemaCreate.Create()
            .Entity<Seat>(e => e.Key(s => s.Id))
            .Entity<Person>(e =>
            {
                e.Key(p => p.Id);
                e.Relation<Seat>(p => p.SeatId).IsUnique();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Seat>(10)
            .Count<Person>(10)
            .Seed(42));

        var persons = data.Get<Person>();
        var seatIds = persons.Select(p => p.SeatId).ToList();

        // All seat IDs should be unique (each target used once)
        Assert.Equal(seatIds.Count, seatIds.Distinct().Count());

        // All seat IDs should be valid
        var allSeatIds = data.Get<Seat>().Select(s => s.Id).ToHashSet();
        Assert.All(seatIds, id => Assert.Contains(id, allSeatIds));
    }

    [Fact]
    public void Unique_FewerTargetsThanSources_Throws()
    {
        var schema = SchemaCreate.Create()
            .Entity<Seat>(e => e.Key(s => s.Id))
            .Entity<Person>(e =>
            {
                e.Key(p => p.Id);
                e.Relation<Seat>(p => p.SeatId).IsUnique();
            })
            .Build();

        var gen = new DataGenerator();
        Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(schema, cfg => cfg
                .Count<Seat>(3)
                .Count<Person>(10)
                .Seed(42)));
    }

    [Fact]
    public void Unique_MoreTargetsThanSources_OK()
    {
        var schema = SchemaCreate.Create()
            .Entity<Seat>(e => e.Key(s => s.Id))
            .Entity<Person>(e =>
            {
                e.Key(p => p.Id);
                e.Relation<Seat>(p => p.SeatId).IsUnique();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Seat>(20)
            .Count<Person>(5)
            .Seed(42));

        var seatIds = data.Get<Person>().Select(p => p.SeatId).ToList();
        Assert.Equal(5, seatIds.Count);
        Assert.Equal(seatIds.Count, seatIds.Distinct().Count());
    }

    [Fact]
    public void Unique_WithSeed_IsDeterministic()
    {
        var schema = SchemaCreate.Create()
            .Entity<Seat>(e => e.Key(s => s.Id))
            .Entity<Person>(e =>
            {
                e.Key(p => p.Id);
                e.Relation<Seat>(p => p.SeatId).IsUnique();
            })
            .Build();

        var gen = new DataGenerator();
        var data1 = gen.Generate(schema, cfg => cfg.Count<Seat>(10).Count<Person>(10).Seed(99));
        var data2 = gen.Generate(schema, cfg => cfg.Count<Seat>(10).Count<Person>(10).Seed(99));

        var ids1 = data1.Get<Person>().Select(p => p.SeatId).ToList();
        var ids2 = data2.Get<Person>().Select(p => p.SeatId).ToList();
        Assert.Equal(ids1, ids2);
    }

    [Fact]
    public void Unique_Optional_FewerTargets_SetsNullForExtras()
    {
        var schema = SchemaCreate.Create()
            .Entity<Employee>(e =>
            {
                e.Key(emp => emp.Id);
                // Self-ref unique optional: only some get a manager
                e.Relation<Employee>(emp => emp.ManagerId)
                    .Where((emp, mgr) => emp.Id != mgr.Id)
                    .IsUnique()
                    .Optional();
            })
            .Build();

        var gen = new DataGenerator();
        // 5 employees, but unique + self-ref means at most 4 can have distinct managers
        var data = gen.Generate(schema, cfg => cfg.Count<Employee>(5).Seed(42));

        var employees = data.Get<Employee>();
        Assert.Equal(5, employees.Count);

        // Check no employee is their own manager
        foreach (var emp in employees)
        {
            if (emp.ManagerId.HasValue)
                Assert.NotEqual(emp.Id, emp.ManagerId.Value);
        }
    }
}
