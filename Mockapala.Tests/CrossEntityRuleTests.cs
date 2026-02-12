using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests cross-entity rules (post-generation validation and repair).
/// </summary>
public class CrossEntityRuleTests
{
    [Fact]
    public void Rule_RepairsData_OrderTotalFromOrderLines()
    {
        var schema = SchemaCreate.Create()
            .Entity<Order>(e => e.Key(o => o.Id))
            .Entity<OrderLine>(e =>
            {
                e.Key(ol => ol.Id);
                e.WithRules(f => f.RuleFor(ol => ol.Amount, f => f.Finance.Amount(1, 100)));
                e.Relation<Order>(ol => ol.OrderId);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Order>(3)
            .Count<OrderLine>(15)
            .Seed(42)
            .PostProcess(result =>
            {
                var orders = result.Get<Order>();
                var orderLines = result.Get<OrderLine>();

                foreach (var order in orders)
                {
                    order.Total = orderLines
                        .Where(ol => ol.OrderId == order.Id)
                        .Sum(ol => ol.Amount);
                }
            }));

        var orders = data.Get<Order>();
        var orderLines = data.Get<OrderLine>();

        foreach (var order in orders)
        {
            var expectedTotal = orderLines
                .Where(ol => ol.OrderId == order.Id)
                .Sum(ol => ol.Amount);
            Assert.Equal(expectedTotal, order.Total);
        }
    }

    [Fact]
    public void Rule_Validates_ThrowsOnInvariantViolation()
    {
        var schema = SchemaCreate.Create()
            .Entity<Order>(e => e.Key(o => o.Id))
            .Build();

        var gen = new DataGenerator();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(schema, cfg => cfg
                .Count<Order>(3)
                .Seed(42)
                .PostProcess(result =>
                {
                    var orders = result.Get<Order>();
                    // All orders have Total = 0, which is an "invariant violation"
                    if (orders.Any(o => o.Total == 0))
                        throw new InvalidOperationException("Order total must be non-zero.");
                })));

        Assert.Contains("non-zero", ex.Message);
    }

    [Fact]
    public void Rule_MultipleRules_RunInOrder()
    {
        var executionOrder = new List<string>();

        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();

        var gen = new DataGenerator();
        gen.Generate(schema, cfg => cfg
            .Count<Company>(1)
            .PostProcess(_ => executionOrder.Add("first"))
            .PostProcess(_ => executionOrder.Add("second"))
            .PostProcess(_ => executionOrder.Add("third")));

        Assert.Equal(new[] { "first", "second", "third" }, executionOrder);
    }
}
