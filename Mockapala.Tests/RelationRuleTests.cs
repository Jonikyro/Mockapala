using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests pair-predicate relation rules: the source entity's own data
/// determines which target entities are eligible for the relation.
/// </summary>
public class RelationRuleTests
{
    // ---------------------------------------------------------------
    // Scenario 1: Region matching
    // An order must ship from a warehouse in the same region.
    // ---------------------------------------------------------------

    [Fact]
    public void RegionMatching_OrderShipsFromSameRegionWarehouse()
    {
        var warehouses = new List<Warehouse>
        {
            new() { Id = 1, Name = "Helsinki Hub", Region = "EU" },
            new() { Id = 2, Name = "Tampere Hub", Region = "EU" },
            new() { Id = 3, Name = "New York Hub", Region = "US" },
            new() { Id = 4, Name = "LA Hub", Region = "US" },
            new() { Id = 5, Name = "Tokyo Hub", Region = "APAC" },
        };

        var schema = SchemaCreate.Create()
            .Entity<Warehouse>(e => e.Key(w => w.Id))
            .Entity<ShipmentOrder>(e =>
            {
                e.Key(o => o.Id);
                e.WithRules(f => f.RuleFor(o => o.Region, f => f.PickRandom("EU", "US", "APAC")));
                e.Relation<Warehouse>(o => o.WarehouseId)
                    .Where((order, warehouse) => order.Region == warehouse.Region);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(warehouses)
            .Count<ShipmentOrder>(30)
            .Seed(42));

        var orders = data.Get<ShipmentOrder>();
        var warehouseById = warehouses.ToDictionary(w => w.Id);

        Assert.Equal(30, orders.Count);
        Assert.All(orders, order =>
        {
            Assert.True(warehouseById.ContainsKey(order.WarehouseId),
                $"Order {order.Id} has invalid WarehouseId {order.WarehouseId}");
            var warehouse = warehouseById[order.WarehouseId];
            Assert.Equal(order.Region, warehouse.Region);
        });
    }

    [Fact]
    public void RegionMatching_AllRegionsGetOrders()
    {
        var warehouses = new List<Warehouse>
        {
            new() { Id = 1, Name = "EU Warehouse", Region = "EU" },
            new() { Id = 2, Name = "US Warehouse", Region = "US" },
        };

        var schema = SchemaCreate.Create()
            .Entity<Warehouse>(e => e.Key(w => w.Id))
            .Entity<ShipmentOrder>(e =>
            {
                e.Key(o => o.Id);
                e.WithRules(f => f.RuleFor(o => o.Region, f => f.PickRandom("EU", "US")));
                e.Relation<Warehouse>(o => o.WarehouseId)
                    .Where((order, warehouse) => order.Region == warehouse.Region);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(warehouses)
            .Count<ShipmentOrder>(40)
            .Seed(42));

        var orders = data.Get<ShipmentOrder>();
        var euOrders = orders.Where(o => o.Region == "EU").ToList();
        var usOrders = orders.Where(o => o.Region == "US").ToList();

        // Both regions should have orders
        Assert.NotEmpty(euOrders);
        Assert.NotEmpty(usOrders);

        // EU orders must point to warehouse 1, US orders to warehouse 2
        Assert.All(euOrders, o => Assert.Equal(1, o.WarehouseId));
        Assert.All(usOrders, o => Assert.Equal(2, o.WarehouseId));
    }

    // ---------------------------------------------------------------
    // Scenario 2: Clearance gating
    // An agent can only be assigned to missions where
    // agent.ClearanceLevel >= mission.RequiredClearance.
    // ---------------------------------------------------------------

    [Fact]
    public void ClearanceGating_AgentOnlyAssignedToAllowedMissions()
    {
        var missions = new List<Mission>
        {
            new() { Id = 1, CodeName = "Recon", RequiredClearance = 1 },
            new() { Id = 2, CodeName = "Infiltrate", RequiredClearance = 2 },
            new() { Id = 3, CodeName = "Sabotage", RequiredClearance = 3 },
            new() { Id = 4, CodeName = "Exfiltrate", RequiredClearance = 4 },
            new() { Id = 5, CodeName = "TopSecret", RequiredClearance = 5 },
        };

        var schema = SchemaCreate.Create()
            .Entity<Mission>(e => e.Key(m => m.Id))
            .Entity<Agent>(e =>
            {
                e.Key(a => a.Id);
                // Agents get clearance 1–5 cycling by generation index
                e.WithRules(f => f.RuleFor(a => a.ClearanceLevel, (f, _) => (f.IndexFaker % 5) + 1));
                e.Relation<Mission>(a => a.MissionId)
                    .Where((agent, mission) => agent.ClearanceLevel >= mission.RequiredClearance);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(missions)
            .Count<Agent>(25)
            .Seed(42));

        var agents = data.Get<Agent>();
        var missionById = missions.ToDictionary(m => m.Id);

        Assert.Equal(25, agents.Count);
        Assert.All(agents, agent =>
        {
            var mission = missionById[agent.MissionId];
            Assert.True(agent.ClearanceLevel >= mission.RequiredClearance,
                $"Agent {agent.Id} (clearance {agent.ClearanceLevel}) assigned to mission " +
                $"'{mission.CodeName}' (requires {mission.RequiredClearance})");
        });
    }

    [Fact]
    public void ClearanceGating_LowestClearanceOnlyGetsLowestMission()
    {
        var missions = new List<Mission>
        {
            new() { Id = 1, CodeName = "Easy", RequiredClearance = 1 },
            new() { Id = 2, CodeName = "Hard", RequiredClearance = 3 },
        };

        var schema = SchemaCreate.Create()
            .Entity<Mission>(e => e.Key(m => m.Id))
            .Entity<Agent>(e =>
            {
                e.Key(a => a.Id);
                e.WithRules(f => f.RuleFor(a => a.ClearanceLevel, _ => 1)); // all clearance-1
                e.Relation<Mission>(a => a.MissionId)
                    .Where((agent, mission) => agent.ClearanceLevel >= mission.RequiredClearance);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(missions)
            .Count<Agent>(10)
            .Seed(42));

        var agents = data.Get<Agent>();
        // All agents have clearance 1, so they can only get mission 1 (RequiredClearance 1)
        Assert.All(agents, a => Assert.Equal(1, a.MissionId));
    }

    [Fact]
    public void ClearanceGating_HighClearanceCanAccessAnyMission()
    {
        var missions = new List<Mission>
        {
            new() { Id = 1, CodeName = "Easy", RequiredClearance = 1 },
            new() { Id = 2, CodeName = "Medium", RequiredClearance = 3 },
            new() { Id = 3, CodeName = "Hard", RequiredClearance = 5 },
        };

        var schema = SchemaCreate.Create()
            .Entity<Mission>(e => e.Key(m => m.Id))
            .Entity<Agent>(e =>
            {
                e.Key(a => a.Id);
                e.WithRules(f => f.RuleFor(a => a.ClearanceLevel, _ => 5)); // max clearance
                e.Relation<Mission>(a => a.MissionId)
                    .Where((agent, mission) => agent.ClearanceLevel >= mission.RequiredClearance);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(missions)
            .Count<Agent>(30)
            .Seed(42));

        var agents = data.Get<Agent>();
        var missionIds = agents.Select(a => a.MissionId).Distinct().OrderBy(x => x).ToList();
        // All 3 missions should appear because clearance 5 qualifies for all
        Assert.Equal(new[] { 1, 2, 3 }, missionIds);
    }

    // ---------------------------------------------------------------
    // Scenario 3: Tier-based feature access
    // A subscription can only enable features at or below its tier.
    // ---------------------------------------------------------------

    [Fact]
    public void TierAccess_SubscriptionOnlyGetsAllowedFeatures()
    {
        var features = new List<Feature>
        {
            new() { Id = 1, Name = "Dashboard", MinTier = 1 },
            new() { Id = 2, Name = "Reports", MinTier = 1 },
            new() { Id = 3, Name = "API Access", MinTier = 2 },
            new() { Id = 4, Name = "Custom Integrations", MinTier = 2 },
            new() { Id = 5, Name = "Dedicated Support", MinTier = 3 },
            new() { Id = 6, Name = "SLA Guarantee", MinTier = 3 },
        };

        var schema = SchemaCreate.Create()
            .Entity<Feature>(e => e.Key(f => f.Id))
            .Entity<Subscription>(e =>
            {
                e.Key(s => s.Id);
                // Cycle through tiers: 1, 2, 3, 1, 2, 3, ...
                e.WithRules(f => f.RuleFor(s => s.Tier, (f, _) => (f.IndexFaker % 3) + 1));
                e.Relation<Feature>(s => s.FeatureId)
                    .Where((sub, feature) => sub.Tier >= feature.MinTier);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(features)
            .Count<Subscription>(30)
            .Seed(42));

        var subscriptions = data.Get<Subscription>();
        var featureById = features.ToDictionary(f => f.Id);

        Assert.All(subscriptions, sub =>
        {
            var feature = featureById[sub.FeatureId];
            Assert.True(sub.Tier >= feature.MinTier,
                $"Subscription {sub.Id} (tier {sub.Tier}) got feature '{feature.Name}' (min tier {feature.MinTier})");
        });
    }

    [Fact]
    public void TierAccess_BasicTierCannotGetEnterpriseFeatures()
    {
        var features = new List<Feature>
        {
            new() { Id = 1, Name = "Dashboard", MinTier = 1 },
            new() { Id = 2, Name = "SLA Guarantee", MinTier = 3 },
        };

        var schema = SchemaCreate.Create()
            .Entity<Feature>(e => e.Key(f => f.Id))
            .Entity<Subscription>(e =>
            {
                e.Key(s => s.Id);
                e.WithRules(f => f.RuleFor(s => s.Tier, _ => 1)); // all basic tier
                e.Relation<Feature>(s => s.FeatureId)
                    .Where((sub, feature) => sub.Tier >= feature.MinTier);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(features)
            .Count<Subscription>(20)
            .Seed(42));

        var subscriptions = data.Get<Subscription>();
        // All basic-tier subs should only get feature 1 (Dashboard, minTier 1)
        Assert.All(subscriptions, s => Assert.Equal(1, s.FeatureId));
    }

    // ---------------------------------------------------------------
    // Scenario 4: Same-department self-referential management
    // A manager must be from the same department as the employee.
    // ---------------------------------------------------------------

    [Fact]
    public void SameDepartmentManager_ManagerMustBeInSameDepartment()
    {
        var schema = SchemaCreate.Create()
            .Entity<DeptEmployee>(e =>
            {
                e.Key(emp => emp.Id);
                e.WithRules(f => f
                    .RuleFor(emp => emp.Name, f => f.Name.FirstName())
                    .RuleFor(emp => emp.Department, f => f.PickRandom("Engineering", "Sales", "HR")));
                e.Relation<DeptEmployee>(emp => emp.ManagerId)
                    .Where((emp, manager) => emp.Department == manager.Department && emp.Id != manager.Id)
                    .Optional();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<DeptEmployee>(30).Seed(42));

        var employees = data.Get<DeptEmployee>();
        var empById = employees.ToDictionary(emp => emp.Id);

        foreach (var emp in employees)
        {
            if (emp.ManagerId.HasValue)
            {
                var manager = empById[emp.ManagerId.Value];
                Assert.Equal(emp.Department, manager.Department);
                Assert.NotEqual(emp.Id, manager.Id);
            }
        }
    }

    [Fact]
    public void SameDepartmentManager_SinglePersonDepartment_GetsNoManager()
    {
        // If only one person in a department, they can't have a same-dept manager
        var schema = SchemaCreate.Create()
            .Entity<DeptEmployee>(e =>
            {
                e.Key(emp => emp.Id);
                // All in unique departments: "Dept-0", "Dept-1", etc. (using Faker index since Id isn't set yet)
                e.WithRules(f => f
                    .RuleFor(emp => emp.Name, f => f.Name.FirstName())
                    .RuleFor(emp => emp.Department, (f, _) => $"Dept-{f.IndexFaker}"));
                e.Relation<DeptEmployee>(emp => emp.ManagerId)
                    .Where((emp, manager) => emp.Department == manager.Department && emp.Id != manager.Id)
                    .Optional();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg.Count<DeptEmployee>(5).Seed(42));

        var employees = data.Get<DeptEmployee>();
        // No one can have a manager (each person is alone in their department)
        Assert.All(employees, emp => Assert.Null(emp.ManagerId));
    }

    // ---------------------------------------------------------------
    // Scenario 5: Multi-condition pair predicate
    // A driver must be insured by a policy that covers both their
    // vehicle class AND their age.
    // ---------------------------------------------------------------

    [Fact]
    public void MultiCondition_DriverMatchesPolicyOnVehicleClassAndAge()
    {
        // Every vehicle class has at least one policy with MinDriverAge <= 18,
        // so any driver (age 18–60) can always find a matching policy.
        var policies = new List<InsurancePolicy>
        {
            new() { Id = 1, CoveredVehicleClass = "Car", MinDriverAge = 18 },
            new() { Id = 2, CoveredVehicleClass = "Car", MinDriverAge = 25 },
            new() { Id = 3, CoveredVehicleClass = "Truck", MinDriverAge = 18 },
            new() { Id = 4, CoveredVehicleClass = "Truck", MinDriverAge = 25 },
            new() { Id = 5, CoveredVehicleClass = "Motorcycle", MinDriverAge = 18 },
        };

        var schema = SchemaCreate.Create()
            .Entity<InsurancePolicy>(e => e.Key(p => p.Id))
            .Entity<InsuredDriver>(e =>
            {
                e.Key(d => d.Id);
                e.WithRules(f => f
                    .RuleFor(d => d.Name, f => f.Name.FirstName())
                    .RuleFor(d => d.VehicleClass, f => f.PickRandom("Car", "Truck", "Motorcycle"))
                    .RuleFor(d => d.Age, f => f.Random.Int(18, 60)));
                e.Relation<InsurancePolicy>(d => d.PolicyId)
                    .Where((driver, policy) =>
                        driver.VehicleClass == policy.CoveredVehicleClass &&
                        driver.Age >= policy.MinDriverAge);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(policies)
            .Count<InsuredDriver>(50)
            .Seed(42));

        var drivers = data.Get<InsuredDriver>();
        var policyById = policies.ToDictionary(p => p.Id);

        Assert.Equal(50, drivers.Count);
        Assert.All(drivers, driver =>
        {
            var policy = policyById[driver.PolicyId];
            Assert.Equal(driver.VehicleClass, policy.CoveredVehicleClass);
            Assert.True(driver.Age >= policy.MinDriverAge,
                $"Driver {driver.Id} (age {driver.Age}, {driver.VehicleClass}) got policy {policy.Id} " +
                $"({policy.CoveredVehicleClass}, min age {policy.MinDriverAge})");
        });
    }

    [Fact]
    public void MultiCondition_YoungTruckDriver_OnlyGetsMatchingPolicy()
    {
        var policies = new List<InsurancePolicy>
        {
            new() { Id = 1, CoveredVehicleClass = "Car", MinDriverAge = 18 },
            new() { Id = 2, CoveredVehicleClass = "Truck", MinDriverAge = 25 },
            new() { Id = 3, CoveredVehicleClass = "Truck", MinDriverAge = 18 },
        };

        var schema = SchemaCreate.Create()
            .Entity<InsurancePolicy>(e => e.Key(p => p.Id))
            .Entity<InsuredDriver>(e =>
            {
                e.Key(d => d.Id);
                e.WithRules(f => f
                    .RuleFor(d => d.VehicleClass, _ => "Truck")
                    .RuleFor(d => d.Age, _ => 20)); // 20 years old truck driver
                e.Relation<InsurancePolicy>(d => d.PolicyId)
                    .Where((driver, policy) =>
                        driver.VehicleClass == policy.CoveredVehicleClass &&
                        driver.Age >= policy.MinDriverAge);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(policies)
            .Count<InsuredDriver>(10)
            .Seed(42));

        var drivers = data.Get<InsuredDriver>();
        // 20-year-old truck driver: can get policy 3 (Truck, min 18) but NOT policy 2 (Truck, min 25)
        Assert.All(drivers, d => Assert.Equal(3, d.PolicyId));
    }

    // ---------------------------------------------------------------
    // Scenario 6: Required relation with predicate — no eligible target
    // ---------------------------------------------------------------

    [Fact]
    public void Required_PredicateExcludesAll_Throws()
    {
        var missions = new List<Mission>
        {
            new() { Id = 1, CodeName = "Impossible", RequiredClearance = 5 },
        };

        var schema = SchemaCreate.Create()
            .Entity<Mission>(e => e.Key(m => m.Id))
            .Entity<Agent>(e =>
            {
                e.Key(a => a.Id);
                e.WithRules(f => f.RuleFor(a => a.ClearanceLevel, _ => 1)); // too low
                e.Relation<Mission>(a => a.MissionId)
                    .Where((agent, mission) => agent.ClearanceLevel >= mission.RequiredClearance);
                    // Required (default) — should throw
            })
            .Build();

        var gen = new DataGenerator();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            gen.Generate(schema, cfg => cfg
                .Prefill(missions)
                .Count<Agent>(5)
                .Seed(42)));

        Assert.Contains("excluded all", ex.Message);
    }

    // ---------------------------------------------------------------
    // Scenario 7: Optional relation with predicate — some sources
    // get a match, others don't (mixed null / non-null FKs)
    // ---------------------------------------------------------------

    [Fact]
    public void Optional_SomeSourcesMatchSomeDont()
    {
        var missions = new List<Mission>
        {
            new() { Id = 1, CodeName = "Easy", RequiredClearance = 1 },
            new() { Id = 2, CodeName = "Hard", RequiredClearance = 5 },
        };

        var schema = SchemaCreate.Create()
            .Entity<Mission>(e => e.Key(m => m.Id))
            .Entity<Agent>(e =>
            {
                e.Key(a => a.Id);
                // Odd IDs get clearance 1, even IDs get clearance 5
                e.WithRules(f => f.RuleFor(a => a.ClearanceLevel, (f, a) => a.Id % 2 == 0 ? 5 : 1));
                e.Relation<Mission>(a => a.MissionId)
                    .Where((agent, mission) => agent.ClearanceLevel == mission.RequiredClearance)
                    .Optional();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(missions)
            .Count<Agent>(10)
            .Seed(42));

        var agents = data.Get<Agent>();

        // Clearance-1 agents get mission 1, clearance-5 agents get mission 2
        foreach (var agent in agents)
        {
            if (agent.ClearanceLevel == 1)
                Assert.Equal(1, agent.MissionId);
            else
                Assert.Equal(2, agent.MissionId);
        }
    }

    // ---------------------------------------------------------------
    // Scenario 8: Two relations on same entity, each with own rule
    // ---------------------------------------------------------------

    [Fact]
    public void TwoRelationsWithDifferentRules_BothApplied()
    {
        var warehouses = new List<Warehouse>
        {
            new() { Id = 1, Name = "EU Warehouse", Region = "EU" },
            new() { Id = 2, Name = "US Warehouse", Region = "US" },
        };

        var features = new List<Feature>
        {
            new() { Id = 1, Name = "Basic Feature", MinTier = 1 },
            new() { Id = 2, Name = "Pro Feature", MinTier = 2 },
        };

        var schema = SchemaCreate.Create()
            .Entity<Warehouse>(e => e.Key(w => w.Id))
            .Entity<Feature>(e => e.Key(f => f.Id))
            .Entity<DualRelEntity>(e =>
            {
                e.Key(d => d.Id);
                e.WithRules(f => f
                    .RuleFor(d => d.Region, _ => "EU")
                    .RuleFor(d => d.Tier, _ => 1));
                // First relation: region-matched warehouse
                e.Relation<Warehouse>(d => d.WarehouseId)
                    .Where((src, wh) => src.Region == wh.Region);
                // Second relation: tier-gated feature
                e.Relation<Feature>(d => d.FeatureId)
                    .Where((src, feat) => src.Tier >= feat.MinTier);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(warehouses)
            .Prefill(features)
            .Count<DualRelEntity>(10)
            .Seed(42));

        var entities = data.Get<DualRelEntity>();
        Assert.All(entities, e =>
        {
            Assert.Equal(1, e.WarehouseId); // EU only
            Assert.Equal(1, e.FeatureId);   // Basic only (tier 1)
        });
    }

    // ---------------------------------------------------------------
    // Scenario 9: Pair predicate with Unique — each eligible target
    // used at most once, constrained by source data
    // ---------------------------------------------------------------

    [Fact]
    public void PairPredicateWithUnique_AssignsDistinctTargets()
    {
        var missions = new List<Mission>
        {
            new() { Id = 1, CodeName = "Alpha", RequiredClearance = 1 },
            new() { Id = 2, CodeName = "Bravo", RequiredClearance = 1 },
            new() { Id = 3, CodeName = "Charlie", RequiredClearance = 1 },
            new() { Id = 4, CodeName = "Delta", RequiredClearance = 3 },
            new() { Id = 5, CodeName = "Echo", RequiredClearance = 3 },
        };

        var schema = SchemaCreate.Create()
            .Entity<Mission>(e => e.Key(m => m.Id))
            .Entity<Agent>(e =>
            {
                e.Key(a => a.Id);
                e.WithRules(f => f.RuleFor(a => a.ClearanceLevel, _ => 1)); // all clearance 1
                e.Relation<Mission>(a => a.MissionId)
                    .Where((agent, mission) => agent.ClearanceLevel >= mission.RequiredClearance)
                    .IsUnique();
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(missions)
            .Count<Agent>(3) // 3 agents, 3 eligible missions (clearance 1)
            .Seed(42));

        var agents = data.Get<Agent>();
        var missionIds = agents.Select(a => a.MissionId).ToList();

        // All missions should be among {1, 2, 3} (clearance-1 missions)
        Assert.All(missionIds, id => Assert.True(id >= 1 && id <= 3));
        // All assignments should be unique
        Assert.Equal(missionIds.Count, missionIds.Distinct().Count());
    }

    // ---------------------------------------------------------------
    // Scenario 10: Determinism — same seed, same pair predicate, same result
    // ---------------------------------------------------------------

    [Fact]
    public void PairPredicate_WithSeed_IsDeterministic()
    {
        var schema = SchemaCreate.Create()
            .Entity<Mission>(e =>
            {
                e.Key(m => m.Id);
                e.WithRules(f => f.RuleFor(m => m.RequiredClearance, (f, m) => ((m.Id - 1) % 3) + 1));
            })
            .Entity<Agent>(e =>
            {
                e.Key(a => a.Id);
                e.WithRules(f => f.RuleFor(a => a.ClearanceLevel, (f, a) => ((a.Id - 1) % 3) + 1));
                e.Relation<Mission>(a => a.MissionId)
                    .Where((agent, mission) => agent.ClearanceLevel >= mission.RequiredClearance);
            })
            .Build();

        var gen = new DataGenerator();
        var data1 = gen.Generate(schema, cfg => cfg.Count<Mission>(6).Count<Agent>(20).Seed(99));
        var data2 = gen.Generate(schema, cfg => cfg.Count<Mission>(6).Count<Agent>(20).Seed(99));

        var ids1 = data1.Get<Agent>().Select(a => a.MissionId).ToList();
        var ids2 = data2.Get<Agent>().Select(a => a.MissionId).ToList();
        Assert.Equal(ids1, ids2);
    }
}

// Helper entity for the two-relation test
public class DualRelEntity
{
    public int Id { get; set; }
    public string Region { get; set; } = string.Empty;
    public int Tier { get; set; }
    public int WarehouseId { get; set; }
    public int FeatureId { get; set; }
}
