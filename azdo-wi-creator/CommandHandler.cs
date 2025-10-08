using System.CommandLine;

namespace AzDoWiCreator;

public static class CommandHandler
{
    public static Command BuildCreateCommand()
    {
        var organizationOption = new Option<string>("--organization", "-o") { Required = true, Description = "Azure DevOps organization URL (e.g., https://dev.azure.com/myorg)" };
        var projectOption = new Option<string>("--project", "-p") { Required = false, Description = "Default Azure DevOps project name (optional if project is specified in spec file per work item)" };
        var workItemTypeOption = new Option<string>("--type", "-t") { Required = true, Description = "Work item type (e.g., Bug, User Story, Task)" };
        var specOption = new Option<string>("--spec", "-s") { Required = true, Description = "Path to JSON specification file (local path, URL, or name like 'feature')" };
        var patOption = new Option<string>("--pat") { Description = "Personal Access Token for authentication. If not provided, uses AZURE_DEVOPS_PAT or interactive browser-based sign-in." };
        var interactiveSignInOption = new Option<bool>("--interactive-signin") { Description = "Force interactive browser-based sign-in, ignoring PAT token and AZURE_DEVOPS_PAT environment variable" };
        var simulateOption = new Option<bool>("--simulate") { Description = "Simulate the operation without making any changes (dry-run mode)" };
        var forceOption = new Option<bool>("--force") { Description = "⚠️  WARNING: Force update of work items even if they don't have the 'azdo-wi-creator' tag. Use with caution!" };
        var newOption = new Option<bool>("--new") { Description = "Always create new work items instead of updating existing ones with the same title" };

        var command = new Command("create", "Create or update work items")
        {
            organizationOption,
            projectOption,
            workItemTypeOption,
            specOption,
            patOption,
            interactiveSignInOption,
            simulateOption,
            forceOption,
            newOption
        };

        command.SetAction((parseResult) =>
        {
            var organization = parseResult.GetValue(organizationOption)!;
            var project = parseResult.GetValue(projectOption);
            var workItemType = parseResult.GetValue(workItemTypeOption)!;
            var specPath = parseResult.GetValue(specOption)!;
            var pat = parseResult.GetValue(patOption);
            var interactiveSignIn = parseResult.GetValue(interactiveSignInOption);
            var simulate = parseResult.GetValue(simulateOption);
            var force = parseResult.GetValue(forceOption);
            var forceNew = parseResult.GetValue(newOption);

            var executor = new WorkItemExecutor(organization, project, workItemType, pat, interactiveSignIn);
            return executor.ExecuteCreateAsync(specPath, simulate, force, forceNew);
        });

        return command;
    }

    public static Command BuildListCommand()
    {
        var organizationOption = new Option<string>("--organization", "-o") { Required = true, Description = "Azure DevOps organization URL (e.g., https://dev.azure.com/myorg)" };
        var projectOption = new Option<string>("--project", "-p") { Required = true, Description = "Azure DevOps project name" };
        var patOption = new Option<string>("--pat") { Description = "Personal Access Token for authentication. If not provided, uses AZURE_DEVOPS_PAT or interactive browser-based sign-in." };
        var interactiveSignInOption = new Option<bool>("--interactive-signin") { Description = "Force interactive browser-based sign-in, ignoring PAT token and AZURE_DEVOPS_PAT environment variable" };
        var areaPathsOption = new Option<bool>("--area-paths") { Description = "List all area paths defined in the Azure DevOps project hierarchically" };
        var fullStringsOption = new Option<bool>("--full-strings") { Description = "When used with --area-paths, output full path strings with quotes and commas (useful for copying to spec files)" };
        var tableOption = new Option<bool>("--table") { Description = "Display work items in a table format with auto-sized columns (ID, Title, State, Area Path, Tags)" };

        var command = new Command("list", "List all work items created by this tool or area paths in the project")
        {
            organizationOption,
            projectOption,
            patOption,
            interactiveSignInOption,
            areaPathsOption,
            fullStringsOption,
            tableOption
        };

        command.SetAction((parseResult) =>
        {
            var organization = parseResult.GetValue(organizationOption)!;
            var project = parseResult.GetValue(projectOption)!;
            var pat = parseResult.GetValue(patOption);
            var interactiveSignIn = parseResult.GetValue(interactiveSignInOption);
            var listAreaPaths = parseResult.GetValue(areaPathsOption);
            var fullStrings = parseResult.GetValue(fullStringsOption);
            var tableFormat = parseResult.GetValue(tableOption);

            var executor = new WorkItemExecutor(organization, project, string.Empty, pat, interactiveSignIn);
            
            if (listAreaPaths)
            {
                return executor.ExecuteListAreaPathsAsync(fullStrings);
            }
            else
            {
                return executor.ExecuteListAsync(tableFormat);
            }
        });

        return command;
    }
}
