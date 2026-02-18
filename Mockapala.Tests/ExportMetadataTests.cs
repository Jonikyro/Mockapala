using Mockapala.Export;
using Mockapala.Schema;
using Xunit;

namespace Mockapala.Tests;

public class ExportMetadataEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CompanyId { get; set; }
}

public class ExportMetadataTests
{
    [Fact]
    public void ToTable_StoresTableNameOnDefinition()
    {
        var schema = SchemaCreate.Create()
            .Entity<ExportMetadataEntity>(e =>
            {
                e.Key(c => c.Id);
                e.ToTable("my_entities");
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ExportMetadataEntity));
        Assert.Equal("my_entities", def.TableName);
    }

    [Fact]
    public void ToTable_NotSet_TableNameIsNull()
    {
        var schema = SchemaCreate.Create()
            .Entity<ExportMetadataEntity>(e => e.Key(c => c.Id))
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ExportMetadataEntity));
        Assert.Null(def.TableName);
    }

    [Fact]
    public void ToTable_NullOrEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            SchemaCreate.Create()
                .Entity<ExportMetadataEntity>(e =>
                {
                    e.Key(c => c.Id);
                    e.ToTable("");
                })
                .Build();
        });

        Assert.Throws<ArgumentException>(() =>
        {
            SchemaCreate.Create()
                .Entity<ExportMetadataEntity>(e =>
                {
                    e.Key(c => c.Id);
                    e.ToTable(null!);
                })
                .Build();
        });
    }

    [Fact]
    public void HasColumnName_StoresColumnNameOnDefinition()
    {
        var schema = SchemaCreate.Create()
            .Entity<ExportMetadataEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.CompanyId).HasColumnName("company_id");
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ExportMetadataEntity));
        Assert.Single(def.ColumnNames);
        Assert.Equal("company_id", def.ColumnNames["CompanyId"]);
    }

    [Fact]
    public void HasColumnName_NotSet_ColumnNamesIsEmpty()
    {
        var schema = SchemaCreate.Create()
            .Entity<ExportMetadataEntity>(e => e.Key(c => c.Id))
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ExportMetadataEntity));
        Assert.Empty(def.ColumnNames);
    }

    [Fact]
    public void HasColumnName_NullOrEmpty_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            SchemaCreate.Create()
                .Entity<ExportMetadataEntity>(e =>
                {
                    e.Key(c => c.Id);
                    e.Property(c => c.Name).HasColumnName("");
                })
                .Build();
        });
    }

    [Fact]
    public void HasColumnName_ChainsWithHasConversion()
    {
        var schema = SchemaCreate.Create()
            .Entity<ExportMetadataEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.Name).HasColumnName("full_name").HasConversion(n => n.ToUpperInvariant());
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ExportMetadataEntity));
        Assert.Equal("full_name", def.ColumnNames["Name"]);
        Assert.Single(def.Conversions);
        Assert.Equal("Name", def.Conversions[0].PropertyName);
    }

    [Fact]
    public void HasConversion_ChainsWithHasColumnName()
    {
        var schema = SchemaCreate.Create()
            .Entity<ExportMetadataEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.Name).HasConversion(n => n.ToUpperInvariant()).HasColumnName("full_name");
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ExportMetadataEntity));
        Assert.Equal("full_name", def.ColumnNames["Name"]);
        Assert.Single(def.Conversions);
    }

    [Fact]
    public void ExportableProperty_UsesColumnNameFromDefinition()
    {
        var schema = SchemaCreate.Create()
            .Entity<ExportMetadataEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.CompanyId).HasColumnName("company_id");
                e.Property(c => c.Name).HasColumnName("full_name");
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ExportMetadataEntity));
        var exportable = ExportableProperty.GetExportableProperties(typeof(ExportMetadataEntity), def);

        var companyProp = exportable.First(p => p.Property.Name == "CompanyId");
        Assert.Equal("company_id", companyProp.ColumnName);

        var nameProp = exportable.First(p => p.Property.Name == "Name");
        Assert.Equal("full_name", nameProp.ColumnName);
    }

    [Fact]
    public void ExportableProperty_DefaultsToPropertyName_WhenNoColumnName()
    {
        var schema = SchemaCreate.Create()
            .Entity<ExportMetadataEntity>(e => e.Key(c => c.Id))
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ExportMetadataEntity));
        var exportable = ExportableProperty.GetExportableProperties(typeof(ExportMetadataEntity), def);

        Assert.All(exportable, p => Assert.Equal(p.Property.Name, p.ColumnName));
    }

    [Fact]
    public void MultipleProperties_HasColumnName_AllStored()
    {
        var schema = SchemaCreate.Create()
            .Entity<ExportMetadataEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.Id).HasColumnName("entity_id");
                e.Property(c => c.Name).HasColumnName("full_name");
                e.Property(c => c.CompanyId).HasColumnName("company_id");
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ExportMetadataEntity));
        Assert.Equal(3, def.ColumnNames.Count);
        Assert.Equal("entity_id", def.ColumnNames["Id"]);
        Assert.Equal("full_name", def.ColumnNames["Name"]);
        Assert.Equal("company_id", def.ColumnNames["CompanyId"]);
    }
}
