global using ExtendedConsole;
global using Console = ExtendedConsole.Console;
using System.Diagnostics;
using System.Reflection;
using RattedSystemsCli.Actions;
using RattedSystemsCli.HostAPI;
using RattedSystemsCli.Overengineering;
using RattedSystemsCli.Utilities;
using TextCopy;

namespace RattedSystemsCli;

class Program
{
    static void Main(string[] args)
    {
        Console.Config.SetupConsole();
        ActionBuilder builder = new ActionBuilder();
        builder.Build(Assembly.GetExecutingAssembly());
        
        CmdLineParser parser = new CmdLineParser("ratted.systems cli", new CmdArg[]
        {
            new CmdArg
            {
                Name = "help",
                Description = "Show this help message"
            },
            new CmdArg
            {
                Name = "has-token",
                Description = "Check if an upload token is configured"
            },
            new CmdArg
            {
                Name = "get-token",
                Description = "Get the currently configured upload token"
            },
            new CmdArg
            {
                Name = "set-token",
                Description = "Set a new upload token",
                ValueDescription = new CmdArgDescription
                {
                    Name = "token",
                    Description = "The upload token to set",
                    Required = true
                }
            },
            new CmdArg
            {
                Name = "upload-file",
                Description = "Upload a file to ratted.systems",
                ValueDescription = new CmdArgDescription
                {
                    Name = "path",
                    Description = "The path to the file to upload",
                    Required = true
                }
            },
            new CmdArg
            {
                Name = "copy-to-clipboard",
                Description = "Copies related output (like the uploaded file url) to the clipboard"
            }
        });
        parser.SetFooter("More functionality coming soon :3");

        CmdArgValueCollection? pargs = null;

        try
        {
            pargs = parser.Parse(args, true);
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
        
        
        builder.Execute(pargs);
        return;
    }
}