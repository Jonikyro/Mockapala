using Microsoft.Data.Sqlite;
using Mockapala.Export.Sqlite;
using Mockapala.Export.Sqlite.Tests.DomainModels;
using Mockapala.Generation;
using Mockapala.Schema;
using Xunit;

namespace Mockapala.Export.Sqlite.Tests;

public class SqliteExporterTests
{
    [Fact]
    public void Export_ThrowsNotSupportedException_WithMessageToUseExportToDatabase()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();
        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg.Count<Company>(1).Seed(1));

        var exporter = new SqliteExporter();
        var ex = Assert.Throws<NotSupportedException>(() =>
            exporter.Export(schema, result, new MemoryStream()));
        Assert.Contains("ExportToDatabase", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExportToDatabase_CreatesTablesAndInsertsRows()
    {
        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Entity<Product>(e => e.Key(p => p.Id))
            .Entity<Customer>(e => e
                .Key(c => c.Id)
                .Relation<Company>(c => c.CompanyId))
            .Entity<Order>(e => e
                .Key(o => o.Id)
                .Relation<Customer>(o => o.CustomerId))
            .Build();

        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg
            .Count<Company>(3)
            .Count<Product>(5)
            .Count<Customer>(10)
            .Count<Order>(20)
            .Seed(42));

        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        // Export using the open connection's connection string won't work for in-memory
        // because a new connection = new empty database. Instead, export directly.
        var exporter = new SqliteExporter();
        // We need to use the same connection. Let's use a shared in-memory DB.
        var connStr = "Data Source=InMemoryTest;Mode=Memory;Cache=Shared";

        using var keepAlive = new SqliteConnection(connStr);
        keepAlive.Open(); // keep the in-memory DB alive

        exporter.ExportToDatabase(schema, result, connStr);

        AssertRowCount(keepAlive, "Company", 3);
        AssertRowCount(keepAlive, "Product", 5);
        AssertRowCount(keepAlive, "Customer", 10);
        AssertRowCount(keepAlive, "Order", 20);

        // Verify tables were created with correct columns
        AssertColumnExists(keepAlive, "Company", "Id");
        AssertColumnExists(keepAlive, "Company", "Name");
        AssertColumnExists(keepAlive, "Product", "Price");
        AssertColumnExists(keepAlive, "Product", "IsActive");
    }

    [Fact]
    public void ExportToDatabase_FilePath_CreatesDatabase()
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"mockapala_test_{Guid.NewGuid():N}.db");
        try
        {
            var schema = SchemaCreate.Create()
                .Entity<Company>(e => e.Key(c => c.Id))
                .Entity<Product>(e => e.Key(p => p.Id))
                .Build();

            var generator = new DataGenerator();
            var result = generator.Generate(schema, cfg => cfg
                .Count<Company>(2)
                .Count<Product>(3)
                .Seed(42));

            var exporter = new SqliteExporter();
            exporter.ExportToDatabase(schema, result, filePath, createIfMissing: true);

            Assert.True(File.Exists(filePath), "SQLite database file should have been created.");

            using (var connection = new SqliteConnection($"Data Source={filePath}"))
            {
                connection.Open();
                AssertRowCount(connection, "Company", 2);
                AssertRowCount(connection, "Product", 3);
            }
        }
        finally
        {
            // Clear connection pool to release file locks before deleting
            SqliteConnection.ClearAllPools();
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    [Fact]
    public void ExportToDatabase_WithCreateTablesFalse_SkipsTableCreation()
    {
        var connStr = "Data Source=NoCreateTest;Mode=Memory;Cache=Shared";
        using var keepAlive = new SqliteConnection(connStr);
        keepAlive.Open();

        // Pre-create the table
        using (var cmd = keepAlive.CreateCommand())
        {
            cmd.CommandText = "CREATE TABLE \"Company\" (\"Id\" INTEGER, \"Name\" TEXT);";
            cmd.ExecuteNonQuery();
        }

        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();

        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg.Count<Company>(3).Seed(42));

        var exporter = new SqliteExporter(new SqliteExportOptions { CreateTables = false });
        exporter.ExportToDatabase(schema, result, connStr);

        AssertRowCount(keepAlive, "Company", 3);
    }

    [Fact]
    public void ExportToDatabase_WithCustomTableNameResolver()
    {
        var connStr = "Data Source=CustomNameTest;Mode=Memory;Cache=Shared";
        using var keepAlive = new SqliteConnection(connStr);
        keepAlive.Open();

        var schema = SchemaCreate.Create()
            .Entity<Company>(e => e.Key(c => c.Id))
            .Build();

        var generator = new DataGenerator();
        var result = generator.Generate(schema, cfg => cfg.Count<Company>(2).Seed(42));

        var exporter = new SqliteExporter(new SqliteExportOptions
        {
            TableNameResolver = t => $"tbl_{t.Name}"
        });
        exporter.ExportToDatabase(schema, result, connStr);

        AssertRowCount(keepAlive, "tbl_Company", 2);
    }

    private static void AssertRowCount(SqliteConnection connection, string tableName, int expectedCount)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT COUNT(*) FROM \"{tableName}\";";
        var count = Convert.ToInt32(cmd.ExecuteScalar());
        Assert.Equal(expectedCount, count);
    }

    private static void AssertColumnExists(SqliteConnection connection, string tableName, string columnName)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info(\"{tableName}\");";
        using var reader = cmd.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
            columns.Add(reader.GetString(1)); // column 1 is "name"
        Assert.Contains(columnName, columns);
    }
}
