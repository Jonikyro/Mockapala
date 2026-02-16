using Mockapala.Export.SqlBulkCopy;
using Mockapala.Export.SqlBulkCopy.Tests.DomainModels;
using Mockapala.Generation;
using Mockapala.Schema;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Mockapala.Export.SqlBulkCopy.Tests;

public class SqlBulkCopyExporterUnitTests
{
    [Fact]
    public void Export_ThrowsNotSupportedException_WithMessageToUseExportToDatabase()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();
        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg.Count<Company>(1).Seed(1));

        var exporter = new SqlBulkCopyExporter();
        var ex = Assert.Throws<NotSupportedException>(() =>
            exporter.Export(schema, result, new MemoryStream()));
        Assert.Contains("ExportToDatabase", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}

[Collection(SqlServerTestCollection.Name)]
public class SqlBulkCopyExporterTests
{
    private readonly SqlServerContainerFixture _fixture;

    public SqlBulkCopyExporterTests(SqlServerContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public void ExportToDatabase_InsertsAllTables()
    {
        var schema = SchemaCreate.Create()
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
            .Build();

        _fixture.ResetDatabase(schema);

        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg
            .Count<Company>(3)
            .Count<Product>(5)
            .Count<Customer>(10)
            .Count<Order>(20)
            .Seed(42));

        var exporter = new SqlBulkCopyExporter(new SqlBulkCopyExportOptions { UseDataReader = true });
        exporter.ExportToDatabase(schema, result, _fixture.ConnectionString);

        AssertRowCount("Company", 3);
        AssertRowCount("Product", 5);
        AssertRowCount("Customer", 10);
        AssertRowCount("Order", 20);
    }

    [Fact]
    public void ExportToDatabase_WithUseDataReader_InsertsRows()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Product>(e => e.Key(p => p.Id))
            .Build();

        _fixture.ResetDatabase(schema);

        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg
            .Count<Company>(2)
            .Count<Product>(3)
            .Seed(42));

        var exporter = new SqlBulkCopyExporter(new SqlBulkCopyExportOptions { UseDataReader = true });
        exporter.ExportToDatabase(schema, result, _fixture.ConnectionString);

        AssertRowCount("Company", 2);
        AssertRowCount("Product", 3);
    }

    [Fact]
    public void ExportToDatabase_WithUseDataReaderFalse_InsertsSameRowCounts()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Product>(e => e.Key(p => p.Id))
            .Build();

        _fixture.ResetDatabase(schema);

        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg
            .Count<Company>(2)
            .Count<Product>(3)
            .Seed(43));

        var exporter = new SqlBulkCopyExporter(new SqlBulkCopyExportOptions { UseDataReader = false });
        exporter.ExportToDatabase(schema, result, _fixture.ConnectionString);

        AssertRowCount("Company", 2);
        AssertRowCount("Product", 3);
    }

    private void AssertRowCount(string tableName, int expectedCount)
    {
        using var connection = new SqlConnection(_fixture.ConnectionString);
        connection.Open();
        using var cmd = new SqlCommand($"SELECT COUNT(*) FROM [{tableName}]", connection);
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.Equal(expectedCount, count);
    }
}
