using Mockapala.Generation;
using Mockapala.Result;
using Mockapala.Schema;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests whether a relation rule between Entity3→Entity4 can be based on
/// a property from Entity1 (an indirect/transitive relation).
///
/// Chain: Department → Team → Project → TaskItem
///
/// Scenario:
///   - Department has a Budget ("High" or "Low").
///   - Team belongs to a Department.
///   - Project belongs to a Team.
///   - TaskItem belongs to a Project.
///   - Goal: TaskItems should only be assigned to Projects whose
///     root Department has Budget == "High".
///
/// The pair predicate on TaskItem→Project is (task, project) => ...
/// but `project` only has `TeamId` (an int FK), not the Team object,
/// and certainly not the Department. So you can't traverse the chain
/// inside the predicate without external help.
/// </summary>
public class IndirectRelationRuleTests
{
    /// <summary>
    /// WORKAROUND: When intermediate entities are prefilled (known data),
    /// the predicate closure can capture lookup dictionaries to traverse
    /// the chain. This works because generation order guarantees that
    /// Project.TeamId is already set by the time TaskItem's relation
    /// is resolved.
    ///
    /// Important: Prefilled entities that already have their FKs set
    /// should NOT declare a Relation in the schema for that FK, because
    /// the generator resolves relations even for prefilled entities
    /// (overwriting the pre-set FK). Here, Teams are prefilled with
    /// DepartmentId already set, so we omit the Team→Department relation.
    /// </summary>
    [Fact]
    public void IndirectRule_WorksWithPrefillAndClosureCapture()
    {
        // --- Prefilled data (known, captured in closure) ---
        var departments = new List<Department>
        {
            new() { Id = 1, Name = "Engineering", Budget = "High" },
            new() { Id = 2, Name = "Marketing", Budget = "Low" },
            new() { Id = 3, Name = "Research", Budget = "High" },
        };

        var teams = new List<Team>
        {
            new() { Id = 1, Name = "Backend", DepartmentId = 1 },   // Engineering (High)
            new() { Id = 2, Name = "Campaigns", DepartmentId = 2 }, // Marketing (Low)
            new() { Id = 3, Name = "ML", DepartmentId = 3 },        // Research (High)
            new() { Id = 4, Name = "Frontend", DepartmentId = 1 },  // Engineering (High)
        };

        // Build lookup dictionaries the predicate will use
        var teamById = teams.ToDictionary(t => t.Id);
        var deptById = departments.ToDictionary(d => d.Id);

        var schema = SchemaCreate.Create()
            .Entity<Department>(e => e.Key(d => d.Id))
            // Team is prefilled with DepartmentId already set.
            // We do NOT declare Relation<Department> here — the generator
            // would overwrite the pre-set FK with a random Department.
            .Entity<Team>(e => e.Key(t => t.Id))
            .Entity<Project>(e =>
            {
                e.Key(p => p.Id);
                e.Relation<Team>(p => p.TeamId);
            })
            .Entity<TaskItem>(e =>
            {
                e.Key(t => t.Id);
                e.Relation<Project>(t => t.ProjectId)
                    .Where((task, project) =>
                    {
                        // project.TeamId is already resolved (Projects generated before Tasks)
                        if (!teamById.TryGetValue(project.TeamId, out var team))
                            return false;
                        if (!deptById.TryGetValue(team.DepartmentId, out var dept))
                            return false;

                        // The indirect rule: only projects under "High" budget departments
                        return dept.Budget == "High";
                    });
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Prefill(departments)
            .Prefill(teams)
            .Count<Project>(10)
            .Count<TaskItem>(30)
            .Seed(42));

        // --- Assert: every task's project traces back to a "High" budget department ---
        var projects = data.Get<Project>();
        var tasks = data.Get<TaskItem>();
        var projectById = projects.ToDictionary(p => p.Id);

        Assert.Equal(30, tasks.Count);
        Assert.All(tasks, task =>
        {
            var project = projectById[task.ProjectId];
            var team = teamById[project.TeamId];
            var dept = deptById[team.DepartmentId];

            Assert.Equal("High", dept.Budget);
        });

        // Verify that "Low" budget department's team (Campaigns, id=2) is never reached
        var usedTeamIds = tasks
            .Select(t => projectById[t.ProjectId].TeamId)
            .Distinct()
            .ToHashSet();

        Assert.DoesNotContain(2, usedTeamIds); // Team "Campaigns" (Marketing/Low) excluded
    }

    /// <summary>
    /// WITHOUT prefill or external lookups, the predicate only sees the
    /// direct (source, target) pair. A predicate that tries to filter by
    /// a grandparent property has no way to look up the chain, because
    /// the target only holds an FK (int), not the referenced object.
    ///
    /// This test demonstrates that the predicate CAN still use
    /// properties on the direct target — but NOT properties from
    /// entities further up the chain.
    /// </summary>
    [Fact]
    public void DirectRule_WorksFine_ButCannotReachGrandparent()
    {
        var schema = SchemaCreate.Create()
            .Entity<Department>(e =>
            {
                e.Key(d => d.Id);
                e.WithRules(f => f
                    .RuleFor(d => d.Name, f => f.Company.CompanyName())
                    .RuleFor(d => d.Budget, f => f.PickRandom("High", "Low")));
            })
            .Entity<Team>(e =>
            {
                e.Key(t => t.Id);
                e.WithRules(f => f.RuleFor(t => t.Name, f => f.Commerce.Department()));
                e.Relation<Department>(t => t.DepartmentId);
            })
            .Entity<Project>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f.RuleFor(p => p.Priority, f => f.PickRandom("Critical", "Normal")));
                e.Relation<Team>(p => p.TeamId);
            })
            .Entity<TaskItem>(e =>
            {
                e.Key(t => t.Id);
                // This predicate CAN filter by the direct target's property...
                e.Relation<Project>(t => t.ProjectId)
                    .Where((task, project) => project.Priority == "Critical");
                // ...but CANNOT filter by Department.Budget without external lookups,
                // because project only has TeamId (int), not the Team/Department objects.
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Department>(3)
            .Count<Team>(6)
            .Count<Project>(12)
            .Count<TaskItem>(24)
            .Seed(42));

        var tasks = data.Get<TaskItem>();
        var projectById = data.Get<Project>().ToDictionary(p => p.Id);

        // The direct predicate works: all tasks point to "Critical" projects
        Assert.All(tasks, task =>
        {
            var project = projectById[task.ProjectId];
            Assert.Equal("Critical", project.Priority);
        });
    }

    /// <summary>
    /// Uses the new 3-arg .Where((source, target, data) => ...) overload to
    /// traverse the chain without prefill or closure-captured lookups.
    /// The generator passes an IGeneratedData snapshot containing all entities
    /// generated so far (guaranteed by topological order).
    /// </summary>
    [Fact]
    public void IndirectRule_WorksWithDataParameter()
    {
        var schema = SchemaCreate.Create()
            .Entity<Department>(e =>
            {
                e.Key(d => d.Id);
                e.WithRules(f => f
                    .RuleFor(d => d.Name, f => f.Company.CompanyName())
                    .RuleFor(d => d.Budget, (f, d) => f.IndexFaker % 2 == 0 ? "High" : "Low"));
            })
            .Entity<Team>(e =>
            {
                e.Key(t => t.Id);
                e.WithRules(f => f.RuleFor(t => t.Name, f => f.Commerce.Department()));
                e.Relation<Department>(t => t.DepartmentId);
            })
            .Entity<Project>(e =>
            {
                e.Key(p => p.Id);
                e.WithRules(f => f.RuleFor(p => p.Priority, f => f.PickRandom("Critical", "Normal")));
                e.Relation<Team>(p => p.TeamId);
            })
            .Entity<TaskItem>(e =>
            {
                e.Key(t => t.Id);
                // The 3-arg predicate: use data to walk the Department→Team→Project chain
                e.Relation<Project>(t => t.ProjectId)
                    .Where((task, project, data) =>
                    {
						var teams = data.Get<Team>();
                        var departments = data.Get<Department>();

                        var team = teams.FirstOrDefault(t => t.Id == project.TeamId);
                        if (team == null) return false;

                        var dept = departments.FirstOrDefault(d => d.Id == team.DepartmentId);
                        if (dept == null) return false;

                        return dept.Budget == "High";
                    });
            })
            .Build();

        var gen = new DataGenerator();
        var result = gen.Generate(schema, cfg => cfg
            .Count<Department>(4)
            .Count<Team>(8)
            .Count<Project>(16)
            .Count<TaskItem>(40));

        // Assert: every task's project traces back to a "High" budget department
        var teams = result.Get<Team>();
        var departments = result.Get<Department>();
        var projects = result.Get<Project>();
        var tasks = result.Get<TaskItem>();

        var teamById = teams.ToDictionary(t => t.Id);
        var deptById = departments.ToDictionary(d => d.Id);
        var projectById = projects.ToDictionary(p => p.Id);

        Assert.Equal(40, tasks.Count);
        Assert.All(tasks, task =>
        {
            var project = projectById[task.ProjectId];
            var team = teamById[project.TeamId];
            var dept = deptById[team.DepartmentId];

            Assert.Equal("High", dept.Budget);
        });

        // Verify at least one "Low" budget department exists (the predicate excluded it)
        Assert.Contains(departments, d => d.Budget == "Low");
    }
}

// ----- Domain models for the test -----

public class Department
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Budget { get; set; } = string.Empty; // "High" or "Low"
}

public class Team
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
}

public class Project
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty; // "Critical" or "Normal"
    public int TeamId { get; set; }
}

public class TaskItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public int ProjectId { get; set; }
}
