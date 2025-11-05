using System.Diagnostics;
using System.Reflection;
using RattedSystemsCli.Utilities;

namespace RattedSystemsCli.Overengineering;

public class ActionBuilder
{
    // instance field to hold the action map
    private Dictionary<ActionAttribute, MethodInfo> _actionMap = new Dictionary<ActionAttribute, MethodInfo>();

    public void Build(Assembly assembly)
    {
        var types = assembly.GetTypes();
        var methodList = new List<(ActionAttribute, MethodInfo)>();

        foreach (var type in types)
        {
            if (type.GetCustomAttribute<ActionerAttribute>() == null)
                continue;
            // todo: consider if methods should be async
            
            var methods = type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);

            foreach (var method in methods)
            {
                var actionAttr = method.GetCustomAttribute<ActionAttribute>();
                if (actionAttr == null)
                    continue;

                methodList.Add((actionAttr, method));
            }
        }

        _actionMap = methodList.ToDictionary(x => x.Item1, x => x.Item2);
    }

    public void Execute(CmdArgValueCollection args)
    {
        foreach (var kvp in _actionMap)
        {
            ActionAttribute actionAttr = kvp.Key;
            string argRequired = actionAttr.ArgRequired;
            ArgRequirement requirementType = actionAttr.RequirementType;
            if (requirementType == ArgRequirement.HasValue && args.HasValue(argRequired) || requirementType == ArgRequirement.HasFlag && args.HasFlag(argRequired))
            {
                MethodInfo method = kvp.Value;
                var instance = Activator.CreateInstance(method.DeclaringType!);
                if (instance == null)
                {
                    Emi.Error("Could not create instance of " + method.DeclaringType!.FullName);
                    continue;
                }

                method.Invoke(instance, new object[] { args });
                return;
            }
        }
        
        Emi.Error("No valid action found for the provided arguments.");
        Environment.ExitCode = 1;
    }
}