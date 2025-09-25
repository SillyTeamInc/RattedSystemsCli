global using ExtendedConsole;
global using Console = ExtendedConsole.Console;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using RattedSystemsCli.Actions;
using RattedSystemsCli.HostAPI;
using RattedSystemsCli.Overengineering;
using RattedSystemsCli.Utilities;
using RattedSystemsCli.Utilities.Github;
using RattedSystemsCli.Utilities.Services;
using TextCopy;

namespace RattedSystemsCli;

// Shut up
#pragma warning disable CS0618 // Type or member is obsolete


class Program
{
    static void Main(string[] args)
    {
        Console.Config.SetupConsole();
        ActionBuilder builder = new ActionBuilder();
        builder.Build(Assembly.GetExecutingAssembly());
        
        CmdLineParser parser = new CmdLineParser($"ratted.systems cli {UpdateChecker.GetCurrentTag()} ({ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit})", new CmdArg[]
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
            },
            new CmdArg
            {
                Name = "run-as-service",
                Description = "Run as a background service.",
                Hidden = true
            },
            new CmdArg
            {
                Name = "service-action",
                Description = "Manage the ratted.systems cli service for auto-uploading files.",
                ValueDescription = new CmdArgDescription
                {
                    Name = "action",
                    Description = "The action to perform on the service (start, stop, restart, status, install, uninstall, configure)",
                    Required = true
                } 
            },
            new CmdArg
            {
                Name = "version",
                Description = "Show version information"
            },
            new CmdArg
            {
                Name = "check-for-updates",
                Description = "Check for updates",
                ValueDescription = new CmdArgDescription
                {
                    Name = "apply",
                    Description = "If specified, will apply the update if one is found",
                    Required = false
                }
            },
            new CmdArg
            {
                Name = "apply-update",
                Description = "Apply an update (used internally by the update process)",
                ValueDescription = new CmdArgDescription
                {
                    Name = "path",
                    Description = "The path to the downloaded update file",
                    Required = true
                },
                Hidden = true
            },
            new CmdArg
            {
                Name = "finish-update",
                Description = "Finish applying an update (used internally by the update process)",
                ValueDescription = new CmdArgDescription
                {
                    Name = "path",
                    Description = "The path to the old executable to be replaced",
                    Required = true
                },
                Hidden = true
            }
        });


        parser.SetFooter($"More functionality coming soon :3");
        
        CmdArgValueCollection? pargs = null;

        try
        {
            pargs = parser.Parse(args, true);
        }
        catch (CommandParserException ex)
        {
            if (ex.Message.IsNullOrEmpty()) return;
            
            Emi.Error(ex.Message);
            parser.ShowHelp();
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine("An unexpected error occurred: " + ex);
            return;
        }
        
        builder.Execute(pargs);
        return;
    }
}