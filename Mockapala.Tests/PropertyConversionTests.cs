using Mockapala.Generation;
using Mockapala.Schema;
using Xunit;

namespace Mockapala.Tests;

public enum OrderStatus { Pending, Shipped, Delivered }

public class ConversionEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Tests property conversion registration and schema-level behavior.
/// </summary>
public class PropertyConversionTests
{
    [Fact]
    public void Property_HasConversion_StoresConversionOnEntity()
    {
        var schema = SchemaCreate.Create()
            .Entity<ConversionEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.Status).HasConversion(s => s.ToString());
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ConversionEntity));
        Assert.Single(def.Conversions);
        Assert.Equal("Status", def.Conversions[0].PropertyName);
        Assert.Equal(typeof(string), def.Conversions[0].ConvertedType);
    }

    [Fact]
    public void Property_HasConversion_ConverterProducesCorrectValue()
    {
        var schema = SchemaCreate.Create()
            .Entity<ConversionEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.Status).HasConversion(s => s.ToString());
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ConversionEntity));
        var converter = def.Conversions[0].Converter;
        Assert.Equal("Shipped", converter(OrderStatus.Shipped));
    }

    [Fact]
    public void Property_MultipleConversions_AllStored()
    {
        var schema = SchemaCreate.Create()
            .Entity<ConversionEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.Status).HasConversion(s => s.ToString());
                e.Property(c => c.CreatedAt).HasConversion(d => d.Ticks);
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ConversionEntity));
        Assert.Equal(2, def.Conversions.Count);
    }

    [Fact]
    public void Property_RedefiningConversion_ReplacesOld()
    {
        var schema = SchemaCreate.Create()
            .Entity<ConversionEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.Status).HasConversion(s => s.ToString());
                e.Property(c => c.Status).HasConversion(s => (int)s);
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ConversionEntity));
        Assert.Single(def.Conversions);
        Assert.Equal(typeof(int), def.Conversions[0].ConvertedType);
    }

    [Fact]
    public void Property_ScalarConversion_OverridesValue()
    {
        var schema = SchemaCreate.Create()
            .Entity<ConversionEntity>(e =>
            {
                e.Key(c => c.Id);
                e.Property(c => c.Name).HasConversion(n => n.ToUpperInvariant());
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ConversionEntity));
        Assert.Single(def.Conversions);
        Assert.Equal(typeof(string), def.Conversions[0].ConvertedType);
        Assert.Equal("HELLO", def.Conversions[0].Converter("hello"));
    }

    [Fact]
    public void Property_NoConversions_EmptyList()
    {
        var schema = SchemaCreate.Create()
            .Entity<ConversionEntity>(e =>
            {
                e.Key(c => c.Id);
            })
            .Build();

        var def = schema.Entities.First(e => e.EntityType == typeof(ConversionEntity));
        Assert.Empty(def.Conversions);
    }
}
