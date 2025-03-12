namespace PlikShare.Core.Utils;

public class UnexpectedOperationResultException: InvalidOperationException
{
    public UnexpectedOperationResultException(
        string operationName,
        Type resultType): base(message: $"Unexpected '{operationName}' result: '{resultType}'")
    {
        
    }
    
    public UnexpectedOperationResultException(
        string operationName,
        string resultValueStr): base(message: $"Unexpected '{operationName}' result: '{resultValueStr}'")
    {
        
    }
}