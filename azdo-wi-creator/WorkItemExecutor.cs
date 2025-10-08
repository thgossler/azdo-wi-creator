using System.Text.Json;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AzDoWiCreator;

public class WorkItemExecutor
{
    private readonly string _organization;
    private readonly string? _project;
    private readonly string _workItemType;
    private readonly string? _pat;
    private readonly bool _interactiveSignIn;

    public WorkItemExecutor(string organization, string? project, string workItemType, string? pat = null, bool interactiveSignIn = false)
    {
        _organization = organization;
        _project = project;
        _workItemType = workItemType;
        _pat = pat;
        _interactiveSignIn = interactiveSignIn;
    }

    public async Task ExecuteCreateAsync(string specPath, bool simulate, bool force, bool forceNew = false)
    {
        try
        {
            // Load spec file
            var content = await SpecFileLoader.LoadSpecContentAsync(specPath);
            var spec = JsonSerializer.Deserialize<WorkItemSpecFile>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

            if (spec == null || spec.WorkItems.Count == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: No work items found in specification file.");
                Console.ResetColor();
                return;
            }

            // Validate that each work item has a project (either from spec or from command line)
            var workItemsWithoutProject = spec.WorkItems
                .Where(wi => string.IsNullOrWhiteSpace(wi.Project) && string.IsNullOrWhiteSpace(_project))
                .ToList();
            
            if (workItemsWithoutProject.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {workItemsWithoutProject.Count} work item(s) do not have a project specified.");
                Console.WriteLine("Either specify --project on command line as default, or add \"project\" field to each work item in the spec file.");
                Console.ResetColor();
                return;
            }

            // Get unique projects involved
            var projects = spec.WorkItems
                .Select(wi => wi.Project ?? _project)
                .Distinct()
                .OrderBy(p => p)
                .ToList();

            Console.WriteLine($"\nLoaded {spec.WorkItems.Count} work item specification(s)");
            Console.WriteLine($"Organization: {_organization}");
            if (projects.Count == 1)
            {
                Console.WriteLine($"Project: {projects[0]}");
            }
            else
            {
                Console.WriteLine($"Projects: {string.Join(", ", projects)} ({projects.Count} projects)");
            }
            Console.WriteLine($"Work Item Type: {_workItemType}");
            Console.WriteLine($"Mode: {(simulate ? "SIMULATION (no changes will be made)" : "EXECUTION")}");
            if (force)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  FORCE MODE ENABLED - Will update work items without tool tag!");
                Console.ResetColor();
            }
            if (forceNew)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("📝 NEW MODE ENABLED - Will always create new work items");
                Console.ResetColor();
            }
            Console.WriteLine();

            if (simulate)
            {
                await SimulateCreationAsync(spec);
            }
            else
            {
                await ExecuteCreationAsync(spec, force, forceNew);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    private Task SimulateCreationAsync(WorkItemSpecFile spec)
    {
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("=== SIMULATION MODE - No changes will be made ===\n");
        Console.ResetColor();

        var totalWorkItems = 0;

        foreach (var workItem in spec.WorkItems)
        {
            // Determine the project for this work item
            var project = workItem.Project ?? _project;
            
            // Resolve field names
            Dictionary<string, object> resolvedFields;
            try
            {
                resolvedFields = FieldNameResolver.ResolveFields(workItem.Fields);
            }
            catch (ArgumentException ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
                Console.ResetColor();
                continue;
            }

            var areaPaths = workItem.AreaPaths.Count > 0 
                ? workItem.AreaPaths 
                : new List<string> { project! };

            foreach (var areaPath in areaPaths)
            {
                totalWorkItems++;
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Would create work item #{totalWorkItems}:");
                Console.ResetColor();
                
                Console.WriteLine($"  Project: {project}");
                Console.WriteLine($"  Type: {_workItemType}");
                Console.WriteLine($"  Area Path: {areaPath}");
                Console.WriteLine($"  State: New");
                
                if (resolvedFields.ContainsKey("System.Title"))
                {
                    Console.WriteLine($"  Title: {resolvedFields["System.Title"]}");
                }

                Console.WriteLine("  Fields:");
                foreach (var field in resolvedFields.OrderBy(f => f.Key))
                {
                    var value = field.Value?.ToString() ?? "(null)";
                    if (value.Length > 100)
                    {
                        value = value.Substring(0, 97) + "...";
                    }
                    Console.WriteLine($"    {field.Key}: {value}");
                    
                    // Check if this field contains markdown
                    if (field.Value is string stringValue && MarkdownHelper.ContainsMarkdownSyntax(stringValue))
                    {
                        if (MarkdownHelper.SupportsHtmlField(field.Key))
                        {
                            var htmlFieldName = $"{field.Key}.Html";
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"      └─ Markdown detected, will also create: {htmlFieldName}");
                            Console.ResetColor();
                        }
                        else
                        {
                            Console.ForegroundColor = ConsoleColor.Cyan;
                            Console.WriteLine($"      └─ Markdown detected, will convert to HTML");
                            Console.ResetColor();
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(workItem.Tags))
                {
                    Console.WriteLine($"  Tags: {AzureDevOpsClient.ToolTag}; {workItem.Tags}");
                }
                else
                {
                    Console.WriteLine($"  Tags: {AzureDevOpsClient.ToolTag}");
                }

                Console.WriteLine();
            }
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"=== SIMULATION COMPLETE ===");
        Console.WriteLine($"Would create {totalWorkItems} work item(s)");
        Console.ResetColor();
        
        return Task.CompletedTask;
    }

    private async Task ExecuteCreationAsync(WorkItemSpecFile spec, bool force, bool forceNew)
    {
        var created = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();

        // Group work items by project for efficient processing
        var workItemsByProject = spec.WorkItems
            .GroupBy(wi => wi.Project ?? _project)
            .ToList();

        foreach (var projectGroup in workItemsByProject)
        {
            var project = projectGroup.Key!;
            Console.WriteLine($"\n--- Processing project: {project} ---");
            
            using var client = new AzureDevOpsClient(_organization, project, _pat, _interactiveSignIn);

            foreach (var workItemSpec in projectGroup)
            {
                // Resolve field names (supports both short names like "Title" and fully qualified names like "System.Title")
                Dictionary<string, object> resolvedFields;
                try
                {
                    resolvedFields = FieldNameResolver.ResolveFields(workItemSpec.Fields);
                }
                catch (ArgumentException ex)
                {
                    errors.Add($"Field resolution error: {ex.Message}");
                    continue;
                }

                // Validate required fields
                if (!resolvedFields.ContainsKey("System.Title"))
                {
                    errors.Add("Work item specification must contain 'System.Title' field (or 'Title')");
                    continue;
                }

                var areaPaths = workItemSpec.AreaPaths.Count > 0 
                    ? workItemSpec.AreaPaths 
                    : new List<string> { project };

                var title = resolvedFields["System.Title"].ToString()!;

                foreach (var areaPath in areaPaths)
                {
                    try
                    {
                    // Check if work item already exists (unless --new flag is used)
                    WorkItem? existingWorkItem = null;
                    if (!forceNew)
                    {
                        existingWorkItem = await client.FindExistingWorkItemAsync(_workItemType, title, areaPath);
                    }

                    if (existingWorkItem != null)
                    {
                        // Work item exists - check for tool tag
                        var hasToolTag = client.HasToolTag(existingWorkItem);

                        if (!hasToolTag && !force)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"⚠️  Skipping: Work item #{existingWorkItem.Id} '{title}' exists but was not created by this tool.");
                            Console.WriteLine($"   Use --force to update anyway (not recommended).");
                            Console.ResetColor();
                            skipped++;
                            continue;
                        }

                        if (!hasToolTag && force)
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"⚠️  Force updating work item #{existingWorkItem.Id} '{title}' (not created by this tool)");
                            Console.ResetColor();
                        }

                                                // Update existing work item
                        Console.WriteLine($"Updating existing work item #{existingWorkItem.Id}: {existingWorkItem.Fields["System.Title"]}");

                        var updatedWorkItem = await client.UpdateWorkItemAsync(
                            existingWorkItem.Id!.Value,
                            resolvedFields,
                            workItemSpec.Tags);

                        Console.ForegroundColor = ConsoleColor.Blue;
                        Console.WriteLine($"✓ Updated work item #{updatedWorkItem.Id}: {title}");
                        Console.WriteLine($"  Area Path: {areaPath}");
                        Console.WriteLine($"  URL: {_organization}/{Uri.EscapeDataString(project)}/_workitems/edit/{updatedWorkItem.Id}");
                        Console.ResetColor();
                        updated++;
                    }
                    else
                    {
                        // Create new work item
                        var createdWorkItem = await client.CreateWorkItemAsync(
                            _workItemType,
                            resolvedFields,
                            areaPath,
                            workItemSpec.Tags);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Created work item #{createdWorkItem.Id}: {title}");
                        Console.WriteLine($"  Area Path: {areaPath}");
                        Console.WriteLine($"  URL: {_organization}/{Uri.EscapeDataString(project)}/_workitems/edit/{createdWorkItem.Id}");
                        Console.ResetColor();
                        created++;
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Error creating work item '{title}' in area path '{areaPath}': {ex.Message}";
                    errors.Add(errorMsg);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ {errorMsg}");
                    Console.ResetColor();
                }
            }
        }
        } // End of projectGroup foreach

        // Print summary
        Console.WriteLine();
        Console.WriteLine("=== SUMMARY ===");
        Console.WriteLine($"Created: {created}");
        Console.WriteLine($"Updated: {updated}");
        if (skipped > 0)
        {
            Console.WriteLine($"Skipped: {skipped}");
        }
        if (errors.Count > 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Errors: {errors.Count}");
            Console.ResetColor();
        }

        if (errors.Count > 0)
        {
            Console.WriteLine("\nErrors encountered:");
            foreach (var error in errors)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"  - {error}");
                Console.ResetColor();
            }
            Environment.Exit(1);
        }
    }

    public async Task ExecuteListAsync(bool tableFormat = false, bool jsonFormat = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_project))
            {
                if (!jsonFormat)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("Error: --project is required for the list command.");
                    Console.ResetColor();
                }
                Environment.Exit(1);
                return;
            }

            using var client = new AzureDevOpsClient(_organization, _project, _pat, _interactiveSignIn, quietMode: jsonFormat);
            var workItems = await client.GetWorkItemsCreatedByToolAsync();

            if (jsonFormat)
            {
                // JSON output - only output the JSON, nothing else
                var jsonItems = workItems.Select(wi => new
                {
                    id = wi.Id ?? 0,
                    title = wi.Fields.ContainsKey("System.Title") ? wi.Fields["System.Title"]?.ToString() ?? "(no title)" : "(no title)",
                    state = wi.Fields.ContainsKey("System.State") ? wi.Fields["System.State"]?.ToString() ?? "(unknown)" : "(unknown)",
                    type = wi.Fields.ContainsKey("System.WorkItemType") ? wi.Fields["System.WorkItemType"]?.ToString() ?? "(unknown)" : "(unknown)",
                    areaPath = wi.Fields.ContainsKey("System.AreaPath") ? wi.Fields["System.AreaPath"]?.ToString() ?? "(unknown)" : "(unknown)",
                    tags = wi.Fields.ContainsKey("System.Tags") ? wi.Fields["System.Tags"]?.ToString() ?? "" : "",
                    url = $"{_organization}/{Uri.EscapeDataString(_project)}/_workitems/edit/{wi.Id ?? 0}"
                }).ToList();

                var options = new JsonSerializerOptions 
                { 
                    WriteIndented = true,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                };
                Console.WriteLine(JsonSerializer.Serialize(jsonItems, options));
                return;
            }

            if (!jsonFormat)
            {
                Console.WriteLine($"\nListing work items created by this tool...");
                Console.WriteLine($"Organization: {_organization}");
                Console.WriteLine($"Project: {_project}");
                Console.WriteLine();
            }

            if (workItems.Count == 0)
            {
                Console.WriteLine("No work items found created by this tool.");
                return;
            }

            Console.WriteLine($"Found {workItems.Count} work item(s):\n");

            if (tableFormat)
            {
                // Collect data for table
                var rows = new List<(int id, string title, string state, string areaPath, string tags)>();
                foreach (var wi in workItems)
                {
                    var id = wi.Id ?? 0;
                    var title = wi.Fields.ContainsKey("System.Title") ? wi.Fields["System.Title"]?.ToString() ?? "(no title)" : "(no title)";
                    var state = wi.Fields.ContainsKey("System.State") ? wi.Fields["System.State"]?.ToString() ?? "(unknown)" : "(unknown)";
                    var areaPath = wi.Fields.ContainsKey("System.AreaPath") ? wi.Fields["System.AreaPath"]?.ToString() ?? "(unknown)" : "(unknown)";
                    var tags = wi.Fields.ContainsKey("System.Tags") ? wi.Fields["System.Tags"]?.ToString() ?? "" : "";
                    
                    rows.Add((id, title, state, areaPath, tags));
                }

                // Calculate column widths
                int idWidth = Math.Max(2, rows.Any() ? rows.Max(r => r.id.ToString().Length) : 2);
                int titleWidth = Math.Max(5, rows.Any() ? rows.Max(r => r.title.Length) : 5);
                int stateWidth = Math.Max(5, rows.Any() ? rows.Max(r => r.state.Length) : 5);
                int areaPathWidth = Math.Max(9, rows.Any() ? rows.Max(r => r.areaPath.Length) : 9);
                int tagsWidth = Math.Max(4, rows.Any() ? rows.Max(r => r.tags.Length) : 4);

                // Print header with proper padding
                var headerParts = new[]
                {
                    "ID".PadRight(idWidth),
                    "Title".PadRight(titleWidth),
                    "State".PadRight(stateWidth),
                    "Area Path".PadRight(areaPathWidth),
                    "Tags".PadRight(tagsWidth)
                };
                Console.WriteLine(string.Join("  ", headerParts));
                
                // Print separator line
                var separatorParts = new[]
                {
                    new string('-', idWidth),
                    new string('-', titleWidth),
                    new string('-', stateWidth),
                    new string('-', areaPathWidth),
                    new string('-', tagsWidth)
                };
                Console.WriteLine(string.Join("  ", separatorParts));

                // Print rows with proper padding
                foreach (var row in rows)
                {
                    var rowParts = new[]
                    {
                        row.id.ToString().PadRight(idWidth),
                        row.title.PadRight(titleWidth),
                        row.state.PadRight(stateWidth),
                        row.areaPath.PadRight(areaPathWidth),
                        row.tags.PadRight(tagsWidth)
                    };
                    Console.WriteLine(string.Join("  ", rowParts));
                }
            }
            else
            {
                foreach (var wi in workItems)
                {
                    var id = wi.Id;
                    var title = wi.Fields.ContainsKey("System.Title") ? wi.Fields["System.Title"] : "(no title)";
                    var state = wi.Fields.ContainsKey("System.State") ? wi.Fields["System.State"] : "(unknown)";
                    var type = wi.Fields.ContainsKey("System.WorkItemType") ? wi.Fields["System.WorkItemType"] : "(unknown)";
                    var areaPath = wi.Fields.ContainsKey("System.AreaPath") ? wi.Fields["System.AreaPath"] : "(unknown)";
                    var tags = wi.Fields.ContainsKey("System.Tags") ? wi.Fields["System.Tags"] : "";

                    Console.WriteLine($"#{id} - {title}");
                    Console.WriteLine($"  Type: {type}");
                    Console.WriteLine($"  State: {state}");
                    Console.WriteLine($"  Area Path: {areaPath}");
                    if (!string.IsNullOrWhiteSpace(tags?.ToString()))
                    {
                        Console.WriteLine($"  Tags: {tags}");
                    }
                    Console.WriteLine($"  URL: {_organization}/{Uri.EscapeDataString(_project)}/_workitems/edit/{id}");
                    Console.WriteLine();
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    public async Task ExecuteListAreaPathsAsync(bool fullStrings = false)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_project))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: --project is required for the list command.");
                Console.ResetColor();
                Environment.Exit(1);
                return;
            }

            Console.WriteLine($"\nListing area paths in project...");
            Console.WriteLine($"Organization: {_organization}");
            Console.WriteLine($"Project: {_project}");
            Console.WriteLine();

            using var client = new AzureDevOpsClient(_organization, _project, _pat, _interactiveSignIn);
            var areaPaths = await client.GetAreaPathsAsync();

            if (areaPaths.Count == 0)
            {
                Console.WriteLine("No area paths found in the project.");
                return;
            }

            Console.WriteLine($"Found {areaPaths.Count} area path(s):\n");

            if (fullStrings)
            {
                // Display as full strings with quotes and commas
                DisplayAreaPathsAsStrings(areaPaths);
            }
            else
            {
                // Display area paths hierarchically
                DisplayAreaPathsHierarchically(areaPaths);
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
            Environment.Exit(1);
        }
    }

    private void DisplayAreaPathsAsStrings(List<string> areaPaths)
    {
        for (int i = 0; i < areaPaths.Count; i++)
        {
            var path = areaPaths[i];
            // Escape backslashes by doubling them
            var escapedPath = path.Replace("\\", "\\\\");
            
            // Add comma for all but the last item
            if (i < areaPaths.Count - 1)
            {
                Console.WriteLine($"\"{escapedPath}\",");
            }
            else
            {
                Console.WriteLine($"\"{escapedPath}\"");
            }
        }
    }

    private void DisplayAreaPathsHierarchically(List<string> areaPaths)
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
    
    private void DisplayNode(AreaPathNode node, int depth)
    {
        var indent = new string(' ', depth * 2);
        Console.WriteLine($"{indent}{node.Name}");
        
        foreach (var child in node.Children.Values.OrderBy(n => n.Name))
        {
            DisplayNode(child, depth + 1);
        }
    }
    
    private class AreaPathNode
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, AreaPathNode> Children { get; set; } = new();
    }
}
