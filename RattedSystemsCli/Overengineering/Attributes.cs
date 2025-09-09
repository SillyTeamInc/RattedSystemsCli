namespace RattedSystemsCli.Overengineering;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public class ActionerAttribute : Attribute
{
    
}


public enum ArgRequirement
{
    HasValue,
    HasFlag
}

[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public class ActionAttribute(string argRequired, ArgRequirement requirementType) : Attribute
{
    public string ArgRequired { get; } = argRequired;
    public ArgRequirement RequirementType { get; } = requirementType;
}


