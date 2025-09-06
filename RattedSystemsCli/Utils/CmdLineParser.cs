namespace RattedSystemsCli.Utils;

public struct CmdArgValue
{
    public string? Name;
    public string? Value;
    
    public bool HasValue => !string.IsNullOrEmpty(Value);
}

public struct CmdArgDescription
{
    public string? Name;
    public string? Description;
    public bool Required;
}

public struct CmdArg
{
    public string? Name;
    public string? Description;
    public CmdArgDescription? ValueDescription;
    
}

// CmdArgValueCollection
public class CmdArgValueCollection : List<CmdArgValue>
{
    public CmdArgValueCollection(IEnumerable<CmdArgValue> collection) : base(collection) { }
    
    public CmdArgValue? this[string name]
    {
        get
        {
            return this.FirstOrDefault(arg => arg.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
        }
    }
    
    public bool GetFlag(string name, bool defaultValue = false)
    {
        return this.Any(arg => arg.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true) || defaultValue;
    }
    
    public bool HasFlag(string name)
    {
        return this.Any(arg => arg.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true);
    }
    
    public string? GetValue(string name, string? defaultValue = null)
    {
        return this.FirstOrDefault(arg => arg.Name?.Equals(name, StringComparison.OrdinalIgnoreCase) == true).Value ?? defaultValue;
    }
}

public class CommandParserException(string message) : Exception(message);

public class CmdLineParser
{
    public string Title { get; private set; }
    public List<CmdArg> Args { get; private set; } = new List<CmdArg>();

    public CmdLineParser(string title, CmdArg[] args)
    {
        Title = title;
        Args.AddRange(args);
    }
    
    public void ShowHelp()
    {
        Console.WriteLine(Title);
        Console.WriteLine("Available arguments:");
        foreach (CmdArg arg in Args)
        {
            // format
            // reuired value args:
            // --arg-name:<value name> - description
            //    <value name> - description
            // optional value args:
            // --arg-name:[value name] - description
            //    <value name> - description
            // flag args:
            // --arg-name - description (for flag args)
            int count = $"--{arg.Name}:".Length;
            string spaces = new string(' ', count);
            
            if (arg.ValueDescription != null && arg.ValueDescription.Value.Required)
            {
                Console.WriteLine($"--{arg.Name}:<{arg.ValueDescription.Value.Name}> - {arg.Description}");
                Console.WriteLine($"{spaces}<{arg.ValueDescription.Value.Name}> - {arg.ValueDescription.Value.Description}");
            }
            else if (arg.ValueDescription != null)
            {
                Console.WriteLine($"--{arg.Name}:[{arg.ValueDescription.Value.Name}] - {arg.Description}");
                Console.WriteLine($"{spaces}[{arg.ValueDescription.Value.Name}] - {arg.ValueDescription.Value.Description}");
            }
            else
            {
                Console.WriteLine($"--{arg.Name} - {arg.Description}");
            }
            
            Console.WriteLine();
        }
    }

    public CmdArgValueCollection Parse(string[] args, bool enforceValidation = false)
    {
        try
        {
            if (args.Length == 0)
            {
                throw new CommandParserException("No arguments defined.");
            }
            
            List<CmdArgValue> parsedArgs = new List<CmdArgValue>();
            
            foreach (string arg in args)
            {
                string[] splitArg = arg.Split(new char[] { ':' }, 2);
                string argName = splitArg[0].TrimStart('-');
                string argValue = splitArg.Length > 1 ? splitArg[1] : string.Empty;
                
                if (enforceValidation && Args.All(a => a.Name?.Equals(argName, StringComparison.OrdinalIgnoreCase) != true))
                {
                    throw new CommandParserException($"Unknown argument: {arg}");
                }

                CmdArg? cmdArg = Args.FirstOrDefault(a => a.Name?.Equals(argName, StringComparison.OrdinalIgnoreCase) == true);
                if (cmdArg != null)
                {
                    if (cmdArg.Value.ValueDescription != null && string.IsNullOrEmpty(argValue) && cmdArg.Value.ValueDescription.Value.Required)
                    {
                        throw new CommandParserException($"Argument '{argName}' requires a value.");
                    }
                    
                    parsedArgs.Add(new CmdArgValue
                    {
                        Name = argName,
                        Value = argValue
                    });
                }
            }

            
            return new CmdArgValueCollection(parsedArgs);
        } catch (Exception ex)
        {
            if (ex is CommandParserException)
            {
                throw;
            }
            else
            {
                throw new CommandParserException("Error parsing command line arguments: " + ex);
            }
        }
    }
    
}