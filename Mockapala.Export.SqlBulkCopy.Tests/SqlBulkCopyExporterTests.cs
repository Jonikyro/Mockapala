using Mockapala.Export.SqlBulkCopy;
using Mockapala.Export.SqlBulkCopy.Tests.DomainModels;
using Mockapala.Generation;
using Mockapala.Schema;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Mockapala.Export.SqlBulkCopy.Tests;

public class SqlBulkCopyExporterTests
{
    private const string LocalTestConnectionString =
        "Server=localhost,1433;Database=TestShit;User Id=sa;Password=Password123!;TrustServerCertificate=true;";

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

    [Fact]
    public void ExportToDatabase_InsertsAllTables()
    {
        var connectionString = Environment.GetEnvironmentVariable("TestSqlServerConnectionString") ?? LocalTestConnectionString;

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

        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg
            .Count<Company>(3)
            .Count<Product>(5)
            .Count<Customer>(10)
            .Count<Order>(20)
            .Seed(42));

        var exporter = new SqlBulkCopyExporter(new SqlBulkCopyExportOptions { UseDataReader = true });
        exporter.ExportToDatabase(schema, result, connectionString);

        AssertRowCount(connectionString, "Company", 3);
        AssertRowCount(connectionString, "Product", 5);
        AssertRowCount(connectionString, "Customer", 10);
        AssertRowCount(connectionString, "Order", 20);
    }

    [Fact]
    public void ExportToDatabase_WithUseDataReader_InsertsRows()
    {
        var connectionString = Environment.GetEnvironmentVariable("TestSqlServerConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Product>(e => e.Key(p => p.Id))
            .Build();
        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg
            .Count<Company>(2)
            .Count<Product>(3)
            .Seed(42));

        var exporter = new SqlBulkCopyExporter(new SqlBulkCopyExportOptions { UseDataReader = true });
        exporter.ExportToDatabase(schema, result, connectionString);

        AssertRowCount(connectionString, "Company", 2);
        AssertRowCount(connectionString, "Product", 3);
    }

    [Fact]
    public void ExportToDatabase_WithUseDataReaderFalse_InsertsSameRowCounts()
    {
        var connectionString = Environment.GetEnvironmentVariable("TestSqlServerConnectionString");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Product>(e => e.Key(p => p.Id))
            .Build();
        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg
            .Count<Company>(2)
            .Count<Product>(3)
            .Seed(43));

        var exporter = new SqlBulkCopyExporter(new SqlBulkCopyExportOptions { UseDataReader = false });
        exporter.ExportToDatabase(schema, result, connectionString);

        AssertRowCount(connectionString, "Company", 2);
        AssertRowCount(connectionString, "Product", 3);
    }

    private static void AssertRowCount(string connectionString, string tableName, int expectedCount)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var cmd = new SqlCommand($"SELECT COUNT(*) FROM [{tableName}]", connection);
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.True(count >= expectedCount,
            $"Table [{tableName}] should have at least {expectedCount} row(s), but has {count}. (Other tests may have inserted rows.)");
    }
}
