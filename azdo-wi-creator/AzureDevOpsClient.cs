using Microsoft.TeamFoundation.WorkItemTracking.WebApi;
using Microsoft.TeamFoundation.WorkItemTracking.WebApi.Models;
using Microsoft.TeamFoundation.Core.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.Client;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.WebApi.Patch;
using Microsoft.VisualStudio.Services.WebApi.Patch.Json;

namespace AzDoWiCreator;

public class AzureDevOpsClient : IDisposable
{
    private readonly VssConnection _connection;
    private readonly WorkItemTrackingHttpClient _witClient;
    private readonly string _project;
    public const string ToolTag = "azdo-wi-creator";

    public AzureDevOpsClient(string organizationUrl, string project, string? pat = null, bool interactiveSignIn = false, bool quietMode = false)
    {
        _project = project;
        
        VssCredentials credentials;
        
        // Priority order: 1) --interactive-signin flag, 2) --pat argument, 3) AZURE_DEVOPS_PAT env var, 4) Interactive browser login
        if (interactiveSignIn)
        {
            // Force interactive browser-based authentication (OAuth)
            if (!quietMode)
            {
                Console.WriteLine("Interactive sign-in requested. Using browser-based authentication...");
                Console.WriteLine("A browser window will open for you to sign in.");
            }
            
            // Use VssOAuthAccessTokenCredential with AAD token
            // This requires the user to be signed in via their browser
            credentials = new VssAadCredential();
        }
        else
        {
            var effectivePat = pat ?? Environment.GetEnvironmentVariable("AZURE_DEVOPS_PAT");
            
            if (!string.IsNullOrEmpty(effectivePat))
            {
                // Use PAT authentication
                credentials = new VssBasicCredential(string.Empty, effectivePat);
                if (!quietMode)
                {
                    Console.WriteLine("Using Personal Access Token for authentication.");
                }
            }
            else
            {
                // Use interactive browser-based authentication (OAuth)
                if (!quietMode)
                {
                    Console.WriteLine("No PAT provided. Using interactive browser-based sign-in...");
                    Console.WriteLine("A browser window will open for you to sign in.");
                }
                
                credentials = new VssAadCredential();
            }
        }

        _connection = new VssConnection(new Uri(organizationUrl), credentials);
        
        // Force connection to authenticate immediately to catch auth errors early
        try
        {
            _connection.ConnectAsync().Wait();
        }
        catch (AggregateException ex)
        {
            var innerEx = ex.InnerException ?? ex;
            throw new InvalidOperationException(
                $"Authentication failed: {innerEx.Message}\n\n" +
                "Interactive authentication with Azure DevOps is not fully supported on macOS.\n" +
                "Please use a Personal Access Token (PAT) instead:\n" +
                $"1. Visit {organizationUrl}/_usersSettings/tokens\n" +
                "2. Create a new token with 'Work Items (Read, Write, & Manage)' scope\n" +
                "3. Use --pat YOUR_TOKEN or set AZURE_DEVOPS_PAT environment variable", 
                innerEx);
        }
        
        _witClient = _connection.GetClient<WorkItemTrackingHttpClient>();
    }

    public async Task<List<WorkItem>> GetWorkItemsCreatedByToolAsync()
    {
        var wiql = new Wiql
        {
            Query = $@"
                SELECT [System.Id], [System.Title], [System.WorkItemType], [System.State], [System.AreaPath], [System.Tags]
                FROM WorkItems
                WHERE [System.TeamProject] = '{_project}'
                  AND [System.Tags] CONTAINS '{ToolTag}'
                ORDER BY [System.Id] DESC"
        };

        var result = await _witClient.QueryByWiqlAsync(wiql, _project);
        
        if (result.WorkItems.Count() == 0)
        {
            return new List<WorkItem>();
        }

        var ids = result.WorkItems.Select(wi => wi.Id).ToArray();
        var workItems = await _witClient.GetWorkItemsAsync(_project, ids, expand: WorkItemExpand.All);
        
        return workItems;
    }

    public async Task<WorkItem?> FindExistingWorkItemAsync(string workItemType, string title, string areaPath)
    {
        // Query for work items with same title, type, and area path
        var wiql = new Wiql
        {
            Query = $@"
                SELECT [System.Id]
                FROM WorkItems
                WHERE [System.TeamProject] = '{_project}'
                  AND [System.WorkItemType] = '{workItemType}'
                  AND [System.Title] = '{title.Replace("'", "''")}'
                  AND [System.AreaPath] = '{areaPath.Replace("'", "''")}'
                ORDER BY [System.ChangedDate] DESC"
        };

        try
        {
            var result = await _witClient.QueryByWiqlAsync(wiql, _project);
            
            if (result.WorkItems.Any())
            {
                var firstId = result.WorkItems.First().Id;
                var workItem = await _witClient.GetWorkItemAsync(_project, firstId, expand: WorkItemExpand.All);
                return workItem;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Error searching for existing work item: {ex.Message}");
        }

        return null;
    }

    public async Task<WorkItem> CreateWorkItemAsync(
        string workItemType,
        Dictionary<string, object> fields,
        string areaPath,
        string? tags = null)
    {
        var patchDocument = new JsonPatchDocument();

        // Add fields and detect markdown
        foreach (var field in fields)
        {
            var value = field.Value;
            
            // If the value is a string and contains markdown/HTML, convert it
            if (value is string stringValue && MarkdownHelper.ContainsMarkdownSyntax(stringValue))
            {
                var htmlValue = MarkdownHelper.ConvertMarkdownToHtml(stringValue);
                
                // Check if this field supports .Html suffix (system fields only)
                if (MarkdownHelper.SupportsHtmlField(field.Key))
                {
                    // For system fields that support .Html, add both the plain text and HTML versions
                    patchDocument.Add(new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{field.Key}",
                        Value = stringValue
                    });
                    
                    var htmlFieldName = $"{field.Key}.Html";
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  └─ Detected markdown in '{field.Key}', adding HTML field '{htmlFieldName}'");
                    Console.ResetColor();

                    patchDocument.Add(new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{htmlFieldName}",
                        Value = htmlValue
                    });
                }
                else
                {
                    // For custom fields, just store the HTML directly in the field
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  └─ Detected markdown in '{field.Key}', converting to HTML");
                    Console.ResetColor();
                    
                    patchDocument.Add(new JsonPatchOperation
                    {
                        Operation = Operation.Add,
                        Path = $"/fields/{field.Key}",
                        Value = htmlValue
                    });
                }
            }
            else
            {
                // Plain text or non-string value, add as-is
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Add,
                    Path = $"/fields/{field.Key}",
                    Value = value
                });
            }
        }

        // Set area path
        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.AreaPath",
            Value = areaPath
        });

        // Set state to "New"
        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.State",
            Value = "New"
        });

        // Add tags
        var allTags = ToolTag;
        if (!string.IsNullOrWhiteSpace(tags))
        {
            allTags = $"{ToolTag}; {tags}";
        }

        patchDocument.Add(new JsonPatchOperation
        {
            Operation = Operation.Add,
            Path = "/fields/System.Tags",
            Value = allTags
        });

        var workItem = await _witClient.CreateWorkItemAsync(patchDocument, _project, workItemType);
        return workItem;
    }

    public async Task<WorkItem> UpdateWorkItemAsync(
        int workItemId,
        Dictionary<string, object> fields,
        string? tags = null,
        bool preserveState = true)
    {
        var patchDocument = new JsonPatchDocument();

        // Get current work item to preserve state if needed
        var currentWorkItem = await _witClient.GetWorkItemAsync(_project, workItemId);
        
        // Update fields and detect markdown
        foreach (var field in fields)
        {
            var value = field.Value;
            
            // If the value is a string and contains markdown/HTML, convert it
            if (value is string stringValue && MarkdownHelper.ContainsMarkdownSyntax(stringValue))
            {
                var htmlValue = MarkdownHelper.ConvertMarkdownToHtml(stringValue);
                
                // Check if this field supports .Html suffix (system fields only)
                if (MarkdownHelper.SupportsHtmlField(field.Key))
                {
                    // For system fields that support .Html, update both the plain text and HTML versions
                    patchDocument.Add(new JsonPatchOperation
                    {
                        Operation = Operation.Replace,
                        Path = $"/fields/{field.Key}",
                        Value = stringValue
                    });
                    
                    var htmlFieldName = $"{field.Key}.Html";
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  └─ Detected markdown in '{field.Key}', updating HTML field '{htmlFieldName}'");
                    Console.ResetColor();

                    patchDocument.Add(new JsonPatchOperation
                    {
                        Operation = Operation.Replace,
                        Path = $"/fields/{htmlFieldName}",
                        Value = htmlValue
                    });
                }
                else
                {
                    // For custom fields, just store the HTML directly in the field
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"  └─ Detected markdown in '{field.Key}', converting to HTML");
                    Console.ResetColor();
                    
                    patchDocument.Add(new JsonPatchOperation
                    {
                        Operation = Operation.Replace,
                        Path = $"/fields/{field.Key}",
                        Value = htmlValue
                    });
                }
            }
            else
            {
                // Plain text or non-string value, update as-is
                patchDocument.Add(new JsonPatchOperation
                {
                    Operation = Operation.Replace,
                    Path = $"/fields/{field.Key}",
                    Value = value
                });
            }
        }

        // Update tags (merge with existing)
        if (!string.IsNullOrWhiteSpace(tags))
        {
            var currentTags = currentWorkItem.Fields.ContainsKey("System.Tags") 
                ? currentWorkItem.Fields["System.Tags"]?.ToString() ?? string.Empty
                : string.Empty;

            var tagsList = currentTags.Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Add new tags
            foreach (var tag in tags.Split(';', ','))
            {
                var trimmedTag = tag.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedTag))
                {
                    tagsList.Add(trimmedTag);
                }
            }

            // Ensure tool tag is present
            tagsList.Add(ToolTag);

            patchDocument.Add(new JsonPatchOperation
            {
                Operation = Operation.Replace,
                Path = "/fields/System.Tags",
                Value = string.Join("; ", tagsList)
            });
        }

        var updatedWorkItem = await _witClient.UpdateWorkItemAsync(patchDocument, workItemId);
        return updatedWorkItem;
    }

    public bool HasToolTag(WorkItem workItem)
    {
        if (!workItem.Fields.ContainsKey("System.Tags"))
            return false;

        var tags = workItem.Fields["System.Tags"]?.ToString() ?? string.Empty;
        var tagsList = tags.Split(';', ',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t));

        return tagsList.Any(t => t.Equals(ToolTag, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<List<string>> GetAreaPathsAsync()
    {
        var projectClient = _connection.GetClient<ProjectHttpClient>();
        var project = await projectClient.GetProject(_project);
        
        // Get the classification nodes (area paths)
        var classificationClient = _connection.GetClient<WorkItemTrackingHttpClient>();
        var areaTree = await classificationClient.GetClassificationNodeAsync(
            _project, 
            TreeStructureGroup.Areas, 
            depth: 100); // Get all levels
        
        var areaPaths = new List<string>();
        CollectAreaPaths(areaTree, areaPaths);
        
        return areaPaths.OrderBy(p => p).ToList();
    }
    
    private void CollectAreaPaths(WorkItemClassificationNode node, List<string> areaPaths, string parentPath = "")
    {
        var currentPath = string.IsNullOrEmpty(parentPath) 
            ? node.Name 
            : $"{parentPath}\\{node.Name}";
        
        // Only add paths beyond the root project node
        if (!string.IsNullOrEmpty(parentPath))
        {
            areaPaths.Add(currentPath);
        }
        
        if (node.HasChildren == true && node.Children != null)
        {
            foreach (var child in node.Children)
            {
                CollectAreaPaths(child, areaPaths, currentPath);
            }
        }
    }

    public void Dispose()
    {
        _witClient?.Dispose();
        _connection?.Dispose();
    }
}
