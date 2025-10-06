using System.CommandLine;
using AzDoWiCreator;

var rootCommand = new RootCommand("Azure DevOps Work Item Creator - Create or update work items from JSON specifications");

// Create command
var createCommand = CommandHandler.BuildCreateCommand();

// List command  
var listCommand = CommandHandler.BuildListCommand();

rootCommand.Subcommands.Add(createCommand);
rootCommand.Subcommands.Add(listCommand);

// If no arguments, show help
if (args.Length == 0)
{
    args = new[] { "--help" };
}
// If first arg is not a command and not an option, prepend "create"
else if (!args[0].StartsWith('-') && args[0] != "create" && args[0] != "list")
{
    var newArgs = new string[args.Length + 1];
    newArgs[0] = "create";
    Array.Copy(args, 0, newArgs, 1, args.Length);
    args = newArgs;
}

return await rootCommand.Parse(args).InvokeAsync();

