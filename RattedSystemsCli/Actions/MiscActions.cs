#pragma warning disable CS0618 // Type or member is obsolete
using RattedSystemsCli.Overengineering;
using RattedSystemsCli.Utilities;

namespace RattedSystemsCli.Actions;



[Actioner]
public class MiscActions 
{
    [Action("version", ArgRequirement.HasFlag)]
    public void VersionAction(CmdArgValueCollection pargs)
    {
        Emi.Info("ratted.systems cli " + ThisAssembly.Git.BaseTag);
        Emi.Info("Commit: " + ThisAssembly.Git.Commit);
        Emi.Info("Branch: " + ThisAssembly.Git.Branch);
        Emi.Info("Repository: " + ThisAssembly.Git.RepositoryUrl);
        string? buildDate = ThisAssembly.Git.CommitDate;
        if (DateTimeOffset.TryParse(buildDate, out var dto))
        {
            string rel = Utils.RelativeTimeSpan((DateTimeOffset.Now - dto));
            Emi.Info($"Commit date: {dto.ToLocalTime():F} ({rel})");
        }
        
        return;
    }
}