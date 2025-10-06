using System.Text.Json;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;

namespace AzDoWiCreator;

public class WorkItemExecutor
{
    private readonly string _organization;
    private readonly string _project;
    private readonly string _workItemType;
    private readonly string? _pat;
    private readonly bool _interactiveSignIn;

    public WorkItemExecutor(string organization, string project, string workItemType, string? pat = null, bool interactiveSignIn = false)
    {
        _organization = organization;
        _project = project;
        _workItemType = workItemType;
        _pat = pat;
        _interactiveSignIn = interactiveSignIn;
    }

    public async Task ExecuteCreateAsync(string specPath, bool simulate, bool force)
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

            Console.WriteLine($"\nLoaded {spec.WorkItems.Count} work item specification(s)");
            Console.WriteLine($"Organization: {_organization}");
            Console.WriteLine($"Project: {_project}");
            Console.WriteLine($"Work Item Type: {_workItemType}");
            Console.WriteLine($"Mode: {(simulate ? "SIMULATION (no changes will be made)" : "EXECUTION")}");
            if (force)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  FORCE MODE ENABLED - Will update work items without tool tag!");
                Console.ResetColor();
            }
            Console.WriteLine();

            if (simulate)
            {
                await SimulateCreationAsync(spec);
            }
            else
            {
                await ExecuteCreationAsync(spec, force);
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
                : new List<string> { _project };

            foreach (var areaPath in areaPaths)
            {
                totalWorkItems++;
                
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Would create work item #{totalWorkItems}:");
                Console.ResetColor();
                
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

    private async Task ExecuteCreationAsync(WorkItemSpecFile spec, bool force)
    {
        using var client = new AzureDevOpsClient(_organization, _project, _pat, _interactiveSignIn);

        var created = 0;
        var updated = 0;
        var skipped = 0;
        var errors = new List<string>();

        foreach (var workItemSpec in spec.WorkItems)
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
                : new List<string> { _project };

            foreach (var areaPath in areaPaths)
            {
                try
                {
                    var title = workItemSpec.Fields["System.Title"].ToString()!;
                    
                    // Check if work item already exists
                    var existingWorkItem = await client.FindExistingWorkItemAsync(_workItemType, title, areaPath);

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
                        Console.WriteLine($"  URL: {_organization}/{_project}/_workitems/edit/{updatedWorkItem.Id}");
                        Console.ResetColor();
                        updated++;
                    }
                    else
                    {
                        // Create new work item
                        var createdWorkItem = await client.CreateWorkItemAsync(
                            _workItemType,
                            workItemSpec.Fields,
                            areaPath,
                            workItemSpec.Tags);

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✓ Created work item #{createdWorkItem.Id}: {title}");
                        Console.WriteLine($"  Area Path: {areaPath}");
                        Console.WriteLine($"  URL: {_organization}/{_project}/_workitems/edit/{createdWorkItem.Id}");
                        Console.ResetColor();
                        created++;
                    }
                }
                catch (Exception ex)
                {
                    var errorMsg = $"Error processing work item for area '{areaPath}': {ex.Message}";
                    errors.Add(errorMsg);
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"✗ {errorMsg}");
                    Console.ResetColor();
                }
            }
        }

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

    public async Task ExecuteListAsync()
    {
        try
        {
            Console.WriteLine($"\nListing work items created by this tool...");
            Console.WriteLine($"Organization: {_organization}");
            Console.WriteLine($"Project: {_project}");
            Console.WriteLine();

            using var client = new AzureDevOpsClient(_organization, _project, _pat, _interactiveSignIn);
            var workItems = await client.GetWorkItemsCreatedByToolAsync();

            if (workItems.Count == 0)
            {
                Console.WriteLine("No work items found created by this tool.");
                return;
            }

            Console.WriteLine($"Found {workItems.Count} work item(s):\n");

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
                Console.WriteLine($"  URL: {_organization}/{_project}/_workitems/edit/{id}");
                Console.WriteLine();
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
}
