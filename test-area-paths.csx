// Simple test to verify the area path display logic
using System;
using System.Collections.Generic;
using System.Linq;

var testPaths = new List<string>
{
    "DS Portfolio Management\\Edge Platform (Edge V2)",
    "DS Portfolio Management\\teamplay Apps\\Analytics Interface Team",
    "DS Portfolio Management\\teamplay Apps\\Contrast Team",
    "DS Portfolio Management\\teamplay Apps\\Dose Team",
    "DS Portfolio Management\\teamplay Apps\\Insights Team",
    "DS Portfolio Management\\teamplay Apps\\Mobile App Team",
    "DS Portfolio Management\\teamplay Apps\\New Images App Team",
    "DS Portfolio Management\\teamplay Apps\\Protocols Team",
    "DS Portfolio Management\\teamplay Apps\\Usage Team",
    "DS Portfolio Management\\teamplay Operations",
    "DS Portfolio Management\\teamplay Platform\\Admin Center Team",
    "DS Portfolio Management\\teamplay Platform\\API Management Team"
};

Console.WriteLine($"Found {testPaths.Count} area path(s):\n");
DisplayAreaPathsHierarchically(testPaths);

void DisplayAreaPathsHierarchically(List<string> areaPaths)
{
    // Build a tree structure from the flat list of paths
    var root = new AreaPathNode();
    
    foreach (var path in areaPaths)
    {
        var parts = path.Split('\\');
        var currentNode = root;
        
        foreach (var part in parts)
        {
            if (!currentNode.Children.ContainsKey(part))
            {
                currentNode.Children[part] = new AreaPathNode { Name = part };
            }
            currentNode = currentNode.Children[part];
        }
    }
    
    // Display the tree with indentation
    foreach (var child in root.Children.Values.OrderBy(n => n.Name))
    {
        DisplayNode(child, 0);
    }
}

void DisplayNode(AreaPathNode node, int depth)
{
    var indent = new string(' ', depth * 2);
    Console.WriteLine($"{indent}{node.Name}");
    
    foreach (var child in node.Children.Values.OrderBy(n => n.Name))
    {
        DisplayNode(child, depth + 1);
    }
}

class AreaPathNode
{
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, AreaPathNode> Children { get; set; } = new();
}
