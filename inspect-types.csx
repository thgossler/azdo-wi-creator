using System;
using System.Reflection;
using System.Linq;

var dll = Assembly.LoadFrom(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + 
    "/.nuget/packages/system.commandline/2.0.0-rc.1.25451.107/lib/net9.0/System.CommandLine.dll");

Console.WriteLine("=== Available Types in System.CommandLine ===");
var types = dll.GetExportedTypes()
    .Where(t => t.IsPublic && !t.Name.Contains("<"))
    .OrderBy(t => t.Name)
    .ToList();

foreach (var type in types)
{
    Console.WriteLine($"{type.FullName}");
}

Console.WriteLine("\n=== Command-related Types ===");
foreach (var type in types.Where(t => t.Name.Contains("Command")))
{
    Console.WriteLine($"\n{type.Name}:");
    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.DeclaringType == type)
        .Take(10);
    foreach (var prop in props)
    {
        Console.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name}");
    }
}

Console.WriteLine("\n=== Option-related Types ===");
foreach (var type in types.Where(t => t.Name.Contains("Option")))
{
    Console.WriteLine($"\n{type.Name}:");
    var props = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(p => p.DeclaringType == type)
        .Take(10);
    foreach (var prop in props)
    {
        Console.WriteLine($"  - {prop.Name}: {prop.PropertyType.Name}");
    }
}
