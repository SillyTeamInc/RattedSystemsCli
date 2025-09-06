global using ExtendedConsole;
global using Console = ExtendedConsole.Console;
global using BetterLogging;
using RattedSystemsCli.Utils;

namespace RattedSystemsCli;

class Program
{
    static void Main(string[] args)
    {
        Console.Config.SetupConsole();
        CmdLineParser parser = new CmdLineParser("ratted.systems cli\n", new CmdArg[]
        {
            new CmdArg
            {
                Name = "help",
                Description = "Show this help message"
            },
        });
        
        CmdArgValueCollection? pargs = null;

        try
        {
            pargs = parser.Parse(args, true);
            // output all parsed args
            foreach (CmdArgValue arg in pargs)
            {
                Emi.Debug(!arg.HasValue ? $"{arg.Name} (flag)" : $"{arg.Name} = {arg.Value}");
            }
        }
        catch (CommandParserException ex)
        {
            Emi.Error(ex.Message);
            parser.ShowHelp();
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine("An unexpected error occurred: " + ex);
            return;
        }
        
        if (pargs.HasFlag("help"))
        {
            parser.ShowHelp();
            return;
        }
        
        
    }
}