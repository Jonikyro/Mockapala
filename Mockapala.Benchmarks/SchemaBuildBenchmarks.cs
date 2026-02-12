using BenchmarkDotNet.Attributes;
using Mockapala.Schema;

namespace Mockapala.Benchmarks;

/// <summary>
/// Measures the cost of building schemas of increasing complexity.
/// Covers entity registration, relation declaration, and topological sort (Build).
/// </summary>
[MemoryDiagnoser]
public class SchemaBuildBenchmarks
{
    // ── Tiny schema: 2 entities, 1 relation ──────────────────────────────────

    [Benchmark(Description = "Schema: 2 entities, 1 relation")]
    public ISchema TinySchema()
    {
        return SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId);
            })
            .Build();
    }

    // ── Small schema: 5 entities, 4 relations (the classic order domain) ─────

    [Benchmark(Description = "Schema: 5 entities, 4 relations")]
    public ISchema SmallSchema()
    {
        return SchemaCreate.Create()
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
    }

    // ── Medium schema: 8 entities, deep chain + branch ───────────────────────

    [Benchmark(Description = "Schema: 8 entities, 7 relations")]
    public ISchema MediumSchema()
    {
        return SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Department>(e =>
            {
                e.Key(d => d.Id);
                e.Relation<Company>(d => d.Budget); // reuse Budget as FK for benchmark
            })
            .Entity<Team>(e =>
            {
                e.Key(t => t.Id);
                e.Relation<Department>(t => t.DepartmentId);
            })
            .Entity<Product>(e => e.Key(p => p.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.Relation<Company>(c => c.CompanyId);
            })
            .Entity<Project>(e =>
            {
                e.Key(p => p.Id);
                e.Relation<Team>(p => p.TeamId);
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
    }

    // ── Schema with relation rules (Where predicates) ────────────────────────

    [Benchmark(Description = "Schema: 5 entities, 4 relations + Where predicates")]
    public ISchema SchemaWithRules()
    {
        return SchemaCreate.Create()
            .Entity<Company>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f.RuleFor(c => c.Name, f2 => f2.Company.CompanyName()));
            })
            .Entity<Product>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f.RuleFor(p => p.Price, f2 => f2.Finance.Amount(1, 100)));
            })
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f.RuleFor(c => c.Name, f2 => f2.Person.FullName));
                e.Relation<Company>(c => c.CompanyId);
            })
            .Entity<Order>(e =>
            {
                e.Key(o => o.Id);
                e.Relation<Customer>(o => o.CustomerId)
                    .Where((order, customer) => customer.IsActive);
            })
            .Entity<OrderLine>(e =>
            {
                e.Key(ol => ol.Id);
                e.Relation<Order>(ol => ol.OrderId);
                e.Relation<Product>(ol => ol.ProductId)
                    .Where((line, product) => product.IsActive && product.Price > 0);
            })
            .Build();
    }

    // ── Schema with self-reference ───────────────────────────────────────────

    [Benchmark(Description = "Schema: self-referential entity")]
    public ISchema SelfReferenceSchema()
    {
        return SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Employee>(e =>
            {
                e.Key(emp => emp.Id);
                e.Relation<Employee>(emp => emp.ManagerId).Optional();
            })
            .Build();
    }
}
