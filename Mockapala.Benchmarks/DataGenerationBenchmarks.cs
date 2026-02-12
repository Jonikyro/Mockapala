using BenchmarkDotNet.Attributes;
using Mockapala.Generation;
using Mockapala.Schema;

namespace Mockapala.Benchmarks;

/// <summary>
/// Measures the cost of generating data at various scales and feature configurations.
/// Each benchmark builds its schema once (GlobalSetup) and then times Generate().
/// </summary>
[MemoryDiagnoser]
public class DataGenerationBenchmarks
{
    // ═══════════════════════════════════════════════════════════════════════
    //  Flat generation (no relations) — measures Bogus + key assignment cost
    // ═══════════════════════════════════════════════════════════════════════

    private ISchema _flatSchema = null!;

    [GlobalSetup(Target = nameof(FlatGeneration_100))]
    public void SetupFlat() => _flatSchema = BuildFlatSchema();

    [GlobalSetup(Target = nameof(FlatGeneration_1_000))]
    public void SetupFlat1K() => _flatSchema = BuildFlatSchema();

    [GlobalSetup(Target = nameof(FlatGeneration_10_000))]
    public void SetupFlat10K() => _flatSchema = BuildFlatSchema();

    private static ISchema BuildFlatSchema() =>
        SchemaCreate.Create()
            .Entity<Product>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f
                    .RuleFor(p => p.Name, f2 => f2.Commerce.ProductName())
                    .RuleFor(p => p.Price, f2 => f2.Finance.Amount(1, 500)));
            })
            .Build();

    [Benchmark(Description = "Flat: 100 products (no relations)")]
    public void FlatGeneration_100()
    {
        new DataGenerator().Generate(_flatSchema, c => c.Count<Product>(100).Seed(42));
    }

    [Benchmark(Description = "Flat: 1K products (no relations)")]
    public void FlatGeneration_1_000()
    {
        new DataGenerator().Generate(_flatSchema, c => c.Count<Product>(1_000).Seed(42));
    }

    [Benchmark(Description = "Flat: 10K products (no relations)")]
    public void FlatGeneration_10_000()
    {
        new DataGenerator().Generate(_flatSchema, c => c.Count<Product>(10_000).Seed(42));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Simple chain (Company → Customer → Order → OrderLine + Product)
    // ═══════════════════════════════════════════════════════════════════════

    private ISchema _chainSchema = null!;

    [GlobalSetup(Targets = new[]
    {
        nameof(Chain_Small), nameof(Chain_Medium), nameof(Chain_Large)
    })]
    public void SetupChain()
    {
        _chainSchema = SchemaCreate.Create()
            .Entity<Company>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f.RuleFor(c => c.Name, f2 => f2.Company.CompanyName()));
            })
            .Entity<Product>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f
                    .RuleFor(p => p.Name, f2 => f2.Commerce.ProductName())
                    .RuleFor(p => p.Price, f2 => f2.Finance.Amount(1, 500)));
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

    [Benchmark(Description = "Chain: 5×10×20×50 (5K OrderLines)")]
    public void Chain_Small()
    {
        new DataGenerator().Generate(_chainSchema, c => c
            .Count<Company>(5)
            .Count<Product>(10)
            .Count<Customer>(10)
            .Count<Order>(20)
            .Count<OrderLine>(50)
            .Seed(42));
    }

    [Benchmark(Description = "Chain: 10×50×100×500 (500 OrderLines)")]
    public void Chain_Medium()
    {
        new DataGenerator().Generate(_chainSchema, c => c
            .Count<Company>(10)
            .Count<Product>(50)
            .Count<Customer>(100)
            .Count<Order>(200)
            .Count<OrderLine>(500)
            .Seed(42));
    }

    [Benchmark(Description = "Chain: 20×100×500×2K (2K OrderLines)")]
    public void Chain_Large()
    {
        new DataGenerator().Generate(_chainSchema, c => c
            .Count<Company>(20)
            .Count<Product>(100)
            .Count<Customer>(500)
            .Count<Order>(1_000)
            .Count<OrderLine>(2_000)
            .Seed(42));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Relations with Where predicates (pair filter cost)
    // ═══════════════════════════════════════════════════════════════════════

    private ISchema _whereSchema = null!;

    [GlobalSetup(Targets = new[]
    {
        nameof(WithWherePredicate_Small), nameof(WithWherePredicate_Medium)
    })]
    public void SetupWhere()
    {
        _whereSchema = SchemaCreate.Create()
            .Entity<Company>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f.RuleFor(c => c.Name, f2 => f2.Company.CompanyName()));
            })
            .Entity<Product>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f
                    .RuleFor(p => p.Name, f2 => f2.Commerce.ProductName())
                    .RuleFor(p => p.Price, f2 => f2.Finance.Amount(0, 200))
                    .RuleFor(p => p.IsActive, f2 => f2.Random.Bool(0.8f)));
            })
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f
                    .RuleFor(c => c.Name, f2 => f2.Person.FullName)
                    .RuleFor(c => c.IsActive, f2 => f2.Random.Bool(0.9f)));
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
                    .Where((line, product) => product.IsActive && product.Price > 10);
            })
            .Build();
    }

    [Benchmark(Description = "Where predicates: 5×10×20×50")]
    public void WithWherePredicate_Small()
    {
        new DataGenerator().Generate(_whereSchema, c => c
            .Count<Company>(5)
            .Count<Product>(20)
            .Count<Customer>(10)
            .Count<Order>(20)
            .Count<OrderLine>(50)
            .Seed(42));
    }

    [Benchmark(Description = "Where predicates: 10×50×100×500")]
    public void WithWherePredicate_Medium()
    {
        new DataGenerator().Generate(_whereSchema, c => c
            .Count<Company>(10)
            .Count<Product>(100)
            .Count<Customer>(50)
            .Count<Order>(100)
            .Count<OrderLine>(500)
            .Seed(42));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Self-referential relation (Employee → Employee manager)
    // ═══════════════════════════════════════════════════════════════════════

    private ISchema _selfRefSchema = null!;

    [GlobalSetup(Targets = new[]
    {
        nameof(SelfReference_100), nameof(SelfReference_1_000)
    })]
    public void SetupSelfRef()
    {
        _selfRefSchema = SchemaCreate.Create()
            .Entity<Employee>(e =>
            {
                e.Key(emp => emp.Id);
                e.WithRules(f => f.RuleFor(emp => emp.Name, f2 => f2.Person.FullName));
                e.Relation<Employee>(emp => emp.ManagerId).Optional();
            })
            .Build();
    }

    [Benchmark(Description = "Self-ref: 100 employees")]
    public void SelfReference_100()
    {
        new DataGenerator().Generate(_selfRefSchema, c => c
            .Count<Employee>(100)
            .Seed(42));
    }

    [Benchmark(Description = "Self-ref: 1K employees")]
    public void SelfReference_1_000()
    {
        new DataGenerator().Generate(_selfRefSchema, c => c
            .Count<Employee>(1_000)
            .Seed(42));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  IdealCount with flexible discard
    // ═══════════════════════════════════════════════════════════════════════

    private ISchema _idealCountSchema = null!;

    [GlobalSetup(Targets = new[]
    {
        nameof(IdealCount_Small), nameof(IdealCount_Medium)
    })]
    public void SetupIdealCount()
    {
        // Only half the products are active → many order lines may get discarded
        _idealCountSchema = SchemaCreate.Create()
            .Entity<Product>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f
                    .RuleFor(p => p.IsActive, f2 => f2.Random.Bool(0.5f))
                    .RuleFor(p => p.Price, f2 => f2.Finance.Amount(1, 100)));
            })
            .Entity<Order>(e => e.Key(o => o.Id))
            .Entity<OrderLine>(e =>
            {
                e.Key(ol => ol.Id);
                e.Relation<Order>(ol => ol.OrderId);
                e.Relation<Product>(ol => ol.ProductId)
                    .Where((line, product) => product.IsActive);
            })
            .Build();
    }

    [Benchmark(Description = "IdealCount: 50 products, 100 lines (min 10)")]
    public void IdealCount_Small()
    {
        new DataGenerator().Generate(_idealCountSchema, c => c
            .Count<Product>(50)
            .Count<Order>(20)
            .IdealCount<OrderLine>(100, min: 10)
            .Seed(42));
    }

    [Benchmark(Description = "IdealCount: 200 products, 1K lines (min 100)")]
    public void IdealCount_Medium()
    {
        new DataGenerator().Generate(_idealCountSchema, c => c
            .Count<Product>(200)
            .Count<Order>(100)
            .IdealCount<OrderLine>(1_000, min: 100)
            .Seed(42));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Deep hierarchy (Department → Team → Project → TaskItem)
    // ═══════════════════════════════════════════════════════════════════════

    private ISchema _deepSchema = null!;

    [GlobalSetup(Targets = new[]
    {
        nameof(DeepHierarchy_Small), nameof(DeepHierarchy_Large)
    })]
    public void SetupDeep()
    {
        _deepSchema = SchemaCreate.Create()
            .Entity<Department>(e =>
            {
                e.Key(d => d.Id);
                e.WithRules(f => f.RuleFor(d => d.Name, f2 => f2.Commerce.Department()));
            })
            .Entity<Team>(e =>
            {
                e.Key(t => t.Id);
                e.WithRules(f => f.RuleFor(t => t.Name, f2 => f2.Company.CompanyName()));
                e.Relation<Department>(t => t.DepartmentId);
            })
            .Entity<Project>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f.RuleFor(p => p.Name, f2 => f2.Hacker.Noun()));
                e.Relation<Team>(p => p.TeamId);
            })
            .Entity<TaskItem>(e =>
            {
                e.Key(t => t.Id);
                e.WithRules(f => f.RuleFor(t => t.Title, f2 => f2.Hacker.Verb() + " " + f2.Hacker.Noun()));
                e.Relation<Project>(t => t.ProjectId);
            })
            .Build();
    }

    [Benchmark(Description = "Deep: 5→10→50→200")]
    public void DeepHierarchy_Small()
    {
        new DataGenerator().Generate(_deepSchema, c => c
            .Count<Department>(5)
            .Count<Team>(10)
            .Count<Project>(50)
            .Count<TaskItem>(200)
            .Seed(42));
    }

    [Benchmark(Description = "Deep: 20→100→500→5K")]
    public void DeepHierarchy_Large()
    {
        new DataGenerator().Generate(_deepSchema, c => c
            .Count<Department>(20)
            .Count<Team>(100)
            .Count<Project>(500)
            .Count<TaskItem>(5_000)
            .Seed(42));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Prefill + generation hybrid
    // ═══════════════════════════════════════════════════════════════════════

    private ISchema _prefillSchema = null!;
    private IReadOnlyList<Company> _prefillCompanies = null!;

    [GlobalSetup(Targets = new[]
    {
        nameof(Prefill_Small), nameof(Prefill_Medium)
    })]
    public void SetupPrefill()
    {
        _prefillCompanies = Enumerable.Range(1, 50)
            .Select(i => new Company { Id = i, Name = $"Company {i}" })
            .ToList();

        _prefillSchema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f.RuleFor(c => c.Name, f2 => f2.Person.FullName));
                e.Relation<Company>(c => c.CompanyId);
            })
            .Entity<Order>(e =>
            {
                e.Key(o => o.Id);
                e.Relation<Customer>(o => o.CustomerId);
            })
            .Build();
    }

    [Benchmark(Description = "Prefill 50 companies + 100 customers + 200 orders")]
    public void Prefill_Small()
    {
        new DataGenerator().Generate(_prefillSchema, c => c
            .Prefill(_prefillCompanies)
            .Count<Customer>(100)
            .Count<Order>(200)
            .Seed(42));
    }

    [Benchmark(Description = "Prefill 50 companies + 500 customers + 2K orders")]
    public void Prefill_Medium()
    {
        new DataGenerator().Generate(_prefillSchema, c => c
            .Prefill(_prefillCompanies)
            .Count<Customer>(500)
            .Count<Order>(2_000)
            .Seed(42));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  PostProcess step overhead
    // ═══════════════════════════════════════════════════════════════════════

    private ISchema _postProcessSchema = null!;

    [GlobalSetup(Targets = new[]
    {
        nameof(PostProcess_500Orders)
    })]
    public void SetupPostProcess()
    {
        _postProcessSchema = SchemaCreate.Create()
            .Entity<Product>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f
                    .RuleFor(p => p.Price, f2 => f2.Finance.Amount(1, 100)));
            })
            .Entity<Customer>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f.RuleFor(c => c.Name, f2 => f2.Person.FullName));
            })
            .Entity<Order>(e =>
            {
                e.Key(o => o.Id);
                e.Relation<Customer>(o => o.CustomerId);
            })
            .Entity<OrderLine>(e =>
            {
                e.Key(ol => ol.Id);
                e.WithRules(f => f.RuleFor(ol => ol.Amount, f2 => f2.Finance.Amount(1, 50)));
                e.Relation<Order>(ol => ol.OrderId);
                e.Relation<Product>(ol => ol.ProductId);
            })
            .Build();
    }

    [Benchmark(Description = "PostProcess: compute Order.Total from 2K lines")]
    public void PostProcess_500Orders()
    {
        new DataGenerator().Generate(_postProcessSchema, c => c
            .Count<Product>(50)
            .Count<Customer>(100)
            .Count<Order>(500)
            .Count<OrderLine>(2_000)
            .Seed(42)
            .PostProcess(data =>
            {
                var lines = data.Get<OrderLine>();
                foreach (var order in data.Get<Order>())
                {
                    order.Total = lines
                        .Where(l => l.OrderId == order.Id)
                        .Sum(l => l.Amount);
                }
            }));
    }

    // ═══════════════════════════════════════════════════════════════════════
    //  Scaling: same schema, increasing entity counts
    // ═══════════════════════════════════════════════════════════════════════

    private ISchema _scaleSchema = null!;

    [GlobalSetup(Targets = new[]
    {
        nameof(Scale_1K_Total), nameof(Scale_10K_Total), nameof(Scale_50K_Total)
    })]
    public void SetupScale()
    {
        _scaleSchema = SchemaCreate.Create()
            .Entity<Company>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f.RuleFor(c => c.Name, f2 => f2.Company.CompanyName()));
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
                e.Relation<Customer>(o => o.CustomerId);
            })
            .Build();
    }

    [Benchmark(Description = "Scale: ~1K total (10+100+1K)")]
    public void Scale_1K_Total()
    {
        new DataGenerator().Generate(_scaleSchema, c => c
            .Count<Company>(10)
            .Count<Customer>(100)
            .Count<Order>(1_000)
            .Seed(42));
    }

    [Benchmark(Description = "Scale: ~10K total (50+500+10K)")]
    public void Scale_10K_Total()
    {
        new DataGenerator().Generate(_scaleSchema, c => c
            .Count<Company>(50)
            .Count<Customer>(500)
            .Count<Order>(10_000)
            .Seed(42));
    }

    [Benchmark(Description = "Scale: ~50K total (100+2K+50K)")]
    public void Scale_50K_Total()
    {
        new DataGenerator().Generate(_scaleSchema, c => c
            .Count<Company>(100)
            .Count<Customer>(2_000)
            .Count<Order>(50_000)
            .Seed(42));
    }
}
