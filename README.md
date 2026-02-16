<img align="right" src="Images/logo-300x300.png" alt="Mockapala logo" width="150" />

# Mockapala

Type-safe test data generator for .NET.Define your entity schema once, and Mockapala generates realistic datasets with automatic FK resolution and business rule filtering. Export the data however you need — use the built-in exporters or write your own.

Built on [Bogus](https://github.com/bchavez/Bogus) for fake data generation.

## Features

- **Relations with FK resolution** — define foreign keys and let the generator wire them up
- **Pair predicates** — filter eligible FK targets by source+target properties (e.g. region matching)
- **Selector strategies** — Random, RoundRobin, SpreadEvenly, or Weighted FK selection
- **Optional FKs** — nullable foreign keys that gracefully handle missing targets
- **Unique relations** — one-to-one constraints (each target used at most once)
- **Flexible counts** — exact counts or ideal counts with minimum threshold
- **Prefill** — supply handwritten entities as inputs to the generator
- **Post-processing** — compute derived fields after generation
- **Property conversions** — transform properties at export time (e.g. `Ulid` → `string`)
- **Extensible export** — implement `IDataExporter` or `ISchemaDataExporter` to export data to any target
- **Deterministic seeds** — reproducible datasets for tests

## Quick Start

```csharp
using Mockapala.Schema;
using Mockapala.Generation;

// 1. Define your entities as plain classes
public class Company
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Customer
{
    public int Id { get; set; }
    public string Email { get; set; }
    public int CompanyId { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public decimal Total { get; set; }
}

// 2. Define schema
var schema = SchemaCreate.Create()
    .Entity<Company>(e => e.Key(c => c.Id))
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
    .Build();

// 3. Generate data
var generator = new DataGenerator();
var data = generator.Generate(schema, cfg => cfg
    .Count<Company>(5)
    .Count<Customer>(50)
    .Count<Order>(200)
    .Seed(42));

// 4. Use it
var companies = data.Get<Company>();  // 5 companies
var customers = data.Get<Customer>(); // 50 customers, each with a valid CompanyId
var orders = data.Get<Order>();       // 200 orders, each with a valid CustomerId
```

## Bogus Rules

Use Bogus to generate realistic property values:

```csharp
.Entity<Customer>(e =>
{
    e.Key(c => c.Id);
    e.WithRules(f => f
        .RuleFor(c => c.Email, f => f.Internet.Email())
        .RuleFor(c => c.Name, f => f.Name.FullName()));
    e.Relation<Company>(c => c.CompanyId);
})
```

## Relations

### Pair Predicates

Filter FK targets based on both source and target properties:

```csharp
.Entity<ShipmentOrder>(e =>
{
    e.Key(o => o.Id);
    e.WithRules(f => f.RuleFor(o => o.Region, f => f.PickRandom("EU", "US", "APAC")));
    e.Relation<Warehouse>(o => o.WarehouseId)
        .Where((order, warehouse) => order.Region == warehouse.Region);
})
```

### Target Predicates

Filter targets without needing access to the source:

```csharp
.Relation<Company>(c => c.CompanyId)
    .WhereTarget(company => company.IsActive)
```

### Indirect Rules (Access Earlier Entities)

Access the full generated dataset in predicates:

```csharp
.Relation<Project>(t => t.ProjectId)
    .Where((task, project, data) =>
    {
        var team = data.Get<Team>().First(t => t.Id == project.TeamId);
        return team.Budget > 100_000;
    })
```

### Selector Strategies

Control how FK targets are picked:

```csharp
// Cycle through targets evenly
.Relation<Company>(c => c.CompanyId)
    .WithStrategy(SelectorStrategy.RoundRobin)

// Weighted random — higher price = picked more often
.Relation<Product>(ol => ol.ProductId)
    .WithWeightedStrategy(p => p.Price)
```

Available strategies: `Random` (default), `RoundRobin`, `SpreadEvenly`, `Weighted`.

### Optional FKs

When no eligible target exists, set the FK to null instead of throwing:

```csharp
.Relation<Company>(c => c.CompanyId)
    .Optional()
```

### Unique Relations (One-to-One)

Each target can only be assigned once:

```csharp
.Relation<Seat>(p => p.SeatId)
    .IsUnique()
```

### Self-Referential Relations

```csharp
.Entity<Employee>(e =>
{
    e.Key(emp => emp.Id);
    e.Relation<Employee>(emp => emp.ManagerId)
        .Where((emp, mgr) => emp.Id != mgr.Id)
        .Optional();
})
```

## Custom Key Generators

```csharp
// Sequential keys starting at 1000
e.Key(o => o.Id).WithGenerator(i => i * 1000)

// Strongly-typed IDs
e.Key(c => c.Id).WithGenerator<int>(i => i, raw => new CustomerId(raw))
```

## Flexible Counts

`IdealCount` generates up to N entities, discarding those that fail relation resolution:

```csharp
var data = generator.Generate(schema, cfg => cfg
    .Count<Company>(10)
    .IdealCount<Customer>(100, min: 20));

// Result: between 20 and 100 customers, depending on how many find eligible companies
```

## Prefill

Supply handwritten entities instead of generating them:

```csharp
var regions = new List<Region>
{
    new() { Id = 1, Name = "Europe" },
    new() { Id = 2, Name = "North America" },
};

var data = generator.Generate(schema, cfg => cfg
    .Prefill(regions)
    .Count<Customer>(50));
```

## Post-Processing

Compute derived fields after generation:

```csharp
var data = generator.Generate(schema, cfg => cfg
    .Count<Order>(10)
    .Count<OrderLine>(50)
    .PostProcess(result =>
    {
        foreach (var order in result.Get<Order>())
        {
            order.Total = result.Get<OrderLine>()
                .Where(l => l.OrderId == order.Id)
                .Sum(l => l.Price * l.Quantity);
        }
    }));
```

## Property Conversions

Transform property values at export time:

```csharp
.Entity<Order>(e =>
{
    e.Key(o => o.Id);
    e.Property(o => o.Status).HasConversion(s => s.ToString()); // enum → string
})
```

Exporters see the converted type via `ExportableProperty.EffectiveType`, so they can map it to the appropriate target type (e.g. `string` → `NVARCHAR(MAX)` or `TEXT`).

## Exporters

Mockapala provides two interfaces for building exporters:

- **`IDataExporter`** — simple exporter that writes `IGeneratedData` to a `Stream`
- **`ISchemaDataExporter`** — schema-aware exporter that receives `ISchema` for entity ordering, metadata, and property conversions

### Writing a Custom Exporter

Use `ExportableProperty.GetExportableProperties()` to get the list of properties to export for each entity. It handles property conversions automatically — call `GetValue(entity)` to read the (possibly converted) value.

```csharp
public class JsonExporter : ISchemaDataExporter
{
    public void Export(ISchema schema, IGeneratedData data, Stream output)
    {
        foreach (var entityType in schema.GenerationOrder)
        {
            var entities = data.Get(entityType);
            var definition = schema.Entities.FirstOrDefault(e => e.EntityType == entityType);
            var properties = ExportableProperty.GetExportableProperties(entityType, definition);

            foreach (var entity in entities)
            {
                foreach (var prop in properties)
                {
                    var value = prop.GetValue(entity);  // applies conversion if defined
                    var type = prop.EffectiveType;       // converted type for inference
                    // write to output...
                }
            }
        }
    }
}
```

### Built-in Exporters

The following exporters ship as separate packages and serve as reference implementations:

| Package | Exporter | Description |
|---------|----------|-------------|
| `Mockapala.Export.Sqlite` | `SqliteExporter` | Creates tables and inserts data into SQLite |
| `Mockapala.Export.SqlBulkCopy` | `SqlBulkCopyExporter` | High-performance bulk insert into SQL Server |

#### SQLite

```csharp
using Mockapala.Export.Sqlite;

var exporter = new SqliteExporter(new SqliteExportOptions
{
    CreateTables = true,
    UseWalMode = true,
});

exporter.ExportToDatabase(schema, data, "testdata.db", createIfMissing: true);
```

#### SQL Server

```csharp
using Mockapala.Export.SqlBulkCopy;

var exporter = new SqlBulkCopyExporter(new SqlBulkCopyExportOptions
{
    UseDataReader = true,
    BatchSize = 1000,
});

exporter.ExportToDatabase(schema, data, "Server=localhost;Database=TestDB;...");
```
