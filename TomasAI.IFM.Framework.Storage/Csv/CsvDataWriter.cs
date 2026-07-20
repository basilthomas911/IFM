using System.Reflection;

namespace TomasAI.IFM.Framework.Storage.Csv;

public class CsvWriter
{
    public static void WriteToCsv<T>(IEnumerable<T> data, string filePath)
    {
        var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

        using var writer = new StreamWriter(filePath);
        // Write header
        writer.WriteLine(string.Join(",", properties.Select(p => p.Name)));

        // Write data
        foreach (var item in data)
        {
            writer.WriteLine(string.Join(",", properties.Select(p => GetPropertyValue(item!, p))));
        }
    }

    static object GetPropertyValue(object obj, PropertyInfo property)
    {
        if (property.PropertyType == typeof(TimeOnly))
        {
            var value = (TimeOnly)property.GetValue(obj, null)!;
            var stringValue =  $"'{value:O}'";
            return stringValue;
        }
        if (property.PropertyType == typeof(DateTime))
        {
            var value = (DateTime)property.GetValue(obj, null)!;
            var stringValue = $"'{value:O}'";
            return stringValue;
        }
        if (property.PropertyType == typeof(string))
        {
            var value = (string)property.GetValue(obj, null)!;
            var stringValue = $"'{value}'";
            return stringValue;
        }
        return property.GetValue(obj, null)!;
    }
}

