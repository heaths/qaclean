using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics.Tracing;
using System.Text.RegularExpressions;
using Azure;
using Azure.AI.Language.QuestionAnswering.Projects;
using Azure.Core.Diagnostics;
using Azure.Identity;

var command = new RootCommand("Cognitive Service - Language sample application for testing new releases.")
{
    new Option<Uri>(
        "--endpoint",
        getDefaultValue: () =>
        {
            if (Uri.TryCreate(Environment.GetEnvironmentVariable("QUESTIONANSWERING_ENDPOINT"), UriKind.Absolute, out var endpoint))
            {
                return endpoint;
            }

            return null;
        },
        description: "Question Answering (formerly QnA Maker) endpoint. The default is the QUESTIONANSWERING_ENDPOINT environment variable.")
    {
        IsRequired = true,
    },

    new Option<string>(
        "--key",
        getDefaultValue: () => Environment.GetEnvironmentVariable("QUESTIONANSWERING_KEY"),
        description: "Question Answering API key. The default is the QUESTIONANSWERING_KEY environment variable, if set; otherwise, current Azure authenticated identity is used, if logged in."),

    new Option<Regex>(
        "--pattern",
        getDefaultValue: () => new Regex("TestProject"))
    {
        IsRequired = true,
    },

    new Option<bool>(
        new[]{"-d", "--debug" },
        "Enable debug logging."),

    new Option<bool>(
        "--dry-run",
        "Show what would happen but do not make any changes."),
};

command.Handler = CommandHandler.Create<Options>(async options =>
{
    using var _debug = options.Debug ? new AzureEventSourceListener(
        (args, message) =>
        {
            var fg = Console.ForegroundColor;
            Console.ForegroundColor = args.Level switch
            {
                EventLevel.Error => ConsoleColor.Red,
                EventLevel.Warning => ConsoleColor.Yellow,
                _ => ConsoleColor.DarkGray,
            };

            try
            {
                Console.Write($"[{args.Level}] ");

                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(message);
            }
            finally
            {
                Console.ForegroundColor = fg;
            }
        },
        EventLevel.Verbose) : null;

    var client = options.CreateClient();
    await foreach (var projectJson in client.GetProjectsAsync())
    {
        var project = projectJson.ToObjectFromJson<Project>(new() { PropertyNameCaseInsensitive = true });
        if (options.Pattern.IsMatch(project.ProjectName))
        {
            if (options.DryRun)
            {
                Console.WriteLine($"WARNING: Would delete {project.ProjectName}");
                continue;
            }
            else
            {
                Console.WriteLine($"WARNING: Deleting {project.ProjectName}");
                await client.DeleteProjectAsync(WaitUntil.Completed, project.ProjectName);
            }
        }
    }
});

return await command.InvokeAsync(args);

class Options
{
    public Uri Endpoint { get; set; }
    public string Key { get; set; }
    public Regex Pattern { get; set; }
    public bool DryRun { get; set; }

    public QuestionAnsweringProjectsClient CreateClient() => string.IsNullOrEmpty(Key) ?
        new QuestionAnsweringProjectsClient(Endpoint, new DefaultAzureCredential()) :
        new QuestionAnsweringProjectsClient(Endpoint, new AzureKeyCredential(Key));

    public bool Debug { get; set; }
}

class Project
{
    public string ProjectName { get; set; }
}