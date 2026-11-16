using System.Text;
using ZoomIN.Platform.Lifecycle;

namespace ZoomIN.Platform.Tests.Lifecycle;

/// <summary>Stands in for schtasks.exe: keeps tasks by name, so no test registers a real task.</summary>
internal sealed class FakeSchtasks : ISchtasks
{
    private readonly Dictionary<string, string> _tasks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Exit code of the next /Create calls; 0 creates the task.</summary>
    public int CreateExitCode { get; set; }

    /// <summary>A failing /Create still registers the task (killed after its work, e.g. a timeout).</summary>
    public bool RegisterDespiteFailure { get; set; }

    /// <summary>Exit code of every /Query call (e.g. -1 for a timeout); null answers from the tasks.</summary>
    public int? QueryExitCode { get; set; }

    /// <summary>Exit code of the next /Delete calls; 0 deletes the task.</summary>
    public int DeleteExitCode { get; set; }

    public IReadOnlyDictionary<string, string> Tasks => _tasks;

    public List<string> Calls { get; } = [];

    /// <summary>The XML file of the last /Create call.</summary>
    public string? LastXmlFile { get; private set; }

    public int Run(IReadOnlyList<string> arguments)
    {
        var verb = arguments[0];
        var name = arguments[2];
        Calls.Add(verb);
        if (verb == "/Create")
        {
            LastXmlFile = arguments[4];
        }

        switch (verb)
        {
            case "/Query" when QueryExitCode is { } code:
                return code;
            case "/Query":
                return _tasks.ContainsKey(name) ? 0 : 1;
            case "/Delete" when DeleteExitCode != 0:
                return DeleteExitCode;
            case "/Delete":
                return _tasks.Remove(name) ? 0 : 1;
            case "/Create" when CreateExitCode == 0:
                // The file exists only during the call, like for the real schtasks.
                _tasks[name] = File.ReadAllText(arguments[4], Encoding.Unicode);
                return 0;
            case "/Create":
                if (RegisterDespiteFailure)
                {
                    _tasks[name] = File.ReadAllText(arguments[4], Encoding.Unicode);
                }

                return CreateExitCode;
            default:
                throw new ArgumentException("Unexpected verb " + verb, nameof(arguments));
        }
    }
}
