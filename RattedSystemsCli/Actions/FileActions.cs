using RattedSystemsCli.HostAPI;
using RattedSystemsCli.HostAPI.SocketUploader;
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
        
        bool specifiedUploadMethod = pargs.HasFlag("upload-method");
        string? uploadMethod = pargs.GetValue("upload-method");

        if (!specifiedUploadMethod)
        {
            // Check if the file is more than (-1) of 100 MB
            var fileInfo = new FileInfo(filePath);
            if (fileInfo.Length > 100 * 1000 * 1000)
            {
                uploadMethod = "socket";
                Emi.Info("File is larger than 100 MB, using socket upload method.");
            }
            else
            {
                uploadMethod = "post";
            }
                
        }
        
        // TODO: Abstract this to be more dynamic and less shit
        Emi.Info("Uploading file: " + filePath);
        try
        {
            if (uploadMethod == "post")
            {
                Emi.Debug("Using POST uploader.");
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
            } else if (uploadMethod == "socket")
            {
                Emi.Debug("Using socket uploader.");
                var uploadTask = SocketUploader.UploadFileAsync(filePath);
                uploadTask.Wait();
                var reply = uploadTask.Result;

                Emi.Info("File uploaded successfully!");
                Emi.Info("File URL: " + reply);
                if (pargs.HasFlag("copy-to-clipboard"))
                {
                    if (!string.IsNullOrWhiteSpace(reply))
                    {
                        Utils.SetClipboardText(reply);
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
                Emi.Error("Invalid upload method specified: " + uploadMethod);
                Environment.ExitCode = 1;
            }
            
            Environment.Exit(Environment.ExitCode);
        }
        catch (Exception ex)
        {
            Emi.Error("An error occurred during file upload: " + ex);
            Environment.ExitCode = 1;
        }

        return;
    }
}