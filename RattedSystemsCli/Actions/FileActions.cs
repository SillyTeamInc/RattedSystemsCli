using RattedSystemsCli.HostAPI;
using RattedSystemsCli.Overengineering;
using RattedSystemsCli.Utilities;

namespace RattedSystemsCli.Actions;

[Actioner]
public class FileActions
{
    [Action("upload-file", ArgRequirement.HasValue)]
    public void UploadFile(CmdArgValueCollection pargs)
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
                        Utils.SetClipboardText(reply.Resource);
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