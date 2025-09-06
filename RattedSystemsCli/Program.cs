global using ExtendedConsole;
global using Console = ExtendedConsole.Console;
global using BetterLogging;
using RattedSystemsCli.HostAPI;
using RattedSystemsCli.Utils;
using TextCopy;

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
                Description = "Copys related output (like the uploaded file url) to the clipboard"
            }
        });

        CmdArgValueCollection? pargs = null;

        try
        {
            pargs = parser.Parse(args, true);
            /*// output all parsed args
            foreach (CmdArgValue arg in pargs)
            {
                Emi.Debug(!arg.HasValue ? $"{arg.Name} (flag)" : $"{arg.Name} = {arg.Value}");
            }*/
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

        if (pargs.HasFlag("has-token"))
        {
            var token = UploadToken.GetToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                Emi.Info("No upload token configured.");
                Emi.Info("You can set it using the --token:<token> argument.");
                Emi.Info("The token is stored at " + UploadToken.GetTokenPath());
                Environment.ExitCode = 1;
            }
            else
            {
                Emi.Info("Upload token is configured.");
                Environment.ExitCode = 0;
            }

            return;
        }

        if (pargs.HasFlag("get-token"))
        {
            var token = UploadToken.GetToken();
            if (string.IsNullOrWhiteSpace(token))
            {
                Emi.Info("No upload token configured.");
                Emi.Info("You can set it using the --token:<token> argument.");
                Emi.Info("The token is stored at " + UploadToken.GetTokenPath());
                Environment.ExitCode = 1;
            }
            else
            {
                Emi.Info("Current upload token: " + token);
                Environment.ExitCode = 0;
            }

            return;
        }

        // if token arg is provided, set it
        if (pargs.HasValue("set-token"))
        {
            string token = pargs.GetValue("set-token") ?? ""; // Assume that it's not null, because Required is true

            UploadToken.SetToken(token);
            Emi.Info("New upload token written to " + UploadToken.GetTokenPath() + "!");

            return;
        }

        if (pargs.HasValue("upload-file"))
        {
            string filePath = pargs.GetValue("upload-file") ?? "";

            if (!File.Exists(filePath))
            {
                Emi.Error("File not found: " + filePath);
                Environment.ExitCode = 1;
                return;
            }

            Emi.Info("Uploading file: " + filePath);
            try
            {
                var uploadTask = Api.UploadFileAsync(filePath);
                uploadTask.Wait();
                var reply = uploadTask.Result;

                if (reply.Success)
                {
                    Emi.Info("File uploaded successfully!");
                    Emi.Info("File URL: " + reply.Resource);
                    if (!string.IsNullOrWhiteSpace(reply.Thumbnail))
                        Emi.Info("Thumbnail URL: " + reply.Thumbnail);
                    if (pargs.HasFlag("copy-to-clipboard"))
                    {
                        if (!string.IsNullOrWhiteSpace(reply.Resource))
                        {
                            Utils.Utils.SetClipboardText(reply.Resource);
                            Emi.Info("File URL copied to clipboard!");
                        }
                        else
                        {
                            Emi.Warn("No file URL to copy to clipboard.");
                        }

                        Environment.ExitCode = 0;
                    }
                }
                else
                {
                    Emi.Error("File upload failed: " + reply.Message);
                    Environment.ExitCode = 1;
                }
            }
            catch (Exception ex)
            {
                Emi.Error("An error occurred during file upload: " + ex);
                Environment.ExitCode = 1;
            }

            return;
        }
    }
}

//