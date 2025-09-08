using RattedSystemsCli.HostAPI;
using RattedSystemsCli.Overengineering;
using RattedSystemsCli.Utilities;

namespace RattedSystemsCli.Actions;

[Actioner]
public class TokenActions
{
    [Action("set-token", ArgRequirement.HasValue)]
    public void SetToken(CmdArgValueCollection pargs)
    {
        var token = pargs.GetValue("set-token");
        UploadToken.SetToken(token ?? throw new ArgumentNullException(nameof(pargs)));
        Emi.Info("New upload token written to " + UploadToken.GetTokenPath() + "!");
    }
    
    [Action("get-token", ArgRequirement.HasFlag)]
    public void GetToken(CmdArgValueCollection pargs)
    {
        bool copyToClipboard = pargs.HasFlag("copy-token");
        var token = UploadToken.GetToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            Emi.Info("No upload token configured.");
            Emi.Info("You can set it using the --set-token:<token> argument.");
            Emi.Info("The token is stored at " + UploadToken.GetTokenPath());
            Environment.ExitCode = 1;
        }
        else
        {
            Emi.Info("Current upload token: " + token);
            if (copyToClipboard)
            {
                try
                {
                    Utils.SetClipboardText(token);
                    Emi.Info("Token copied to clipboard!");
                }
                catch (Exception ex)
                {
                    Emi.Error("Failed to copy token to clipboard: " + ex.Message);
                }
            }
            Environment.ExitCode = 0;
        }
    }
    
    [Action("has-token", ArgRequirement.HasFlag)]
    public void HasToken(CmdArgValueCollection pargs)
    {
        var token = UploadToken.GetToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            Emi.Info("No upload token configured.");
            Emi.Info("You can set it using the --set-token:<token> argument.");
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
}