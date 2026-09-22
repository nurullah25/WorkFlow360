namespace WorkFlow360.Application.Common.Exceptions;

/// <summary>The request is well-formed but breaks a business rule, e.g. not enough leave balance.</summary>
public class BusinessRuleException : Exception
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
