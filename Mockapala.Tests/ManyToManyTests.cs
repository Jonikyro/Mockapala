using Mockapala.Generation;
using Mockapala.Schema;
using Mockapala.Tests.DomainModels;
using Xunit;

namespace Mockapala.Tests;

/// <summary>
/// Tests many-to-many relations via junction entity with relation rules.
/// </summary>
public class ManyToManyTests
{
    [Fact]
    public void ManyToMany_JunctionEntity_ResolvesBothFKs()
    {
        var schema = SchemaCreate.Create()
            .Entity<Student>(e =>
            {
                e.Key(s => s.Id);
                e.WithRules(f => f.RuleFor(s => s.Name, f => f.Name.FirstName()));
            })
            .Entity<Course>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f.RuleFor(c => c.Title, f => f.Lorem.Word()));
            })
            .Entity<Enrollment>(e =>
            {
                e.Key(en => en.Id);
                e.Relation<Student>(en => en.StudentId);
                e.Relation<Course>(en => en.CourseId);
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Student>(5)
            .Count<Course>(3)
            .Count<Enrollment>(15)
            .Seed(42));

        var students = data.Get<Student>();
        var courses = data.Get<Course>();
        var enrollments = data.Get<Enrollment>();

        var studentIds = students.Select(s => s.Id).ToHashSet();
        var courseIds = courses.Select(c => c.Id).ToHashSet();

        Assert.Equal(15, enrollments.Count);
        Assert.All(enrollments, en =>
        {
            Assert.Contains(en.StudentId, studentIds);
            Assert.Contains(en.CourseId, courseIds);
        });
    }

    [Fact]
    public void ManyToMany_WithRules_OnlyActiveStudentsEnrolled()
    {
        var schema = SchemaCreate.Create()
            .Entity<Student>(e =>
            {
                e.Key(s => s.Id);
                e.WithRules(f => f
                    .RuleFor(s => s.Name, f => f.Name.FirstName())
                    .RuleFor(s => s.IsActive, (f, s) => s.Id % 2 == 0)); // even IDs are active
            })
            .Entity<Course>(e =>
            {
                e.Key(c => c.Id);
                e.WithRules(f => f
                    .RuleFor(c => c.Title, f => f.Lorem.Word())
                    .RuleFor(c => c.IsOpen, _ => true));
            })
            .Entity<Enrollment>(e =>
            {
                e.Key(en => en.Id);
                e.Relation<Student>(en => en.StudentId)
                    .WhereTarget(s => s.IsActive); // only active students
                e.Relation<Course>(en => en.CourseId)
                    .WhereTarget(c => c.IsOpen); // only open courses
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Student>(10)
            .Count<Course>(4)
            .Count<Enrollment>(20)
            .Seed(42));

        var students = data.Get<Student>();
        var enrollments = data.Get<Enrollment>();
        var activeStudentIds = students.Where(s => s.IsActive).Select(s => s.Id).ToHashSet();

        Assert.All(enrollments, en => Assert.Contains(en.StudentId, activeStudentIds));
    }

    [Fact]
    public void ManyToMany_WithPairPredicate()
    {
        // Rule: enrollment connects student to course, but student ID must be < course ID * 3
        // (contrived, but tests pair predicate)
        var schema = SchemaCreate.Create()
            .Entity<Student>(e => e.Key(s => s.Id))
            .Entity<Course>(e => e.Key(c => c.Id))
            .Entity<Enrollment>(e =>
            {
                e.Key(en => en.Id);
                e.Relation<Student>(en => en.StudentId);
                e.Relation<Course>(en => en.CourseId)
                    .Where((enrollment, course) => course.Id <= 3); // only courses 1-3
            })
            .Build();

        var gen = new DataGenerator();
        var data = gen.Generate(schema, cfg => cfg
            .Count<Student>(5)
            .Count<Course>(5)
            .Count<Enrollment>(10)
            .Seed(42));

        var enrollments = data.Get<Enrollment>();
        Assert.All(enrollments, en => Assert.True(en.CourseId <= 3));
    }
}
