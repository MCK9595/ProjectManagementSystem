namespace ProjectManagementSystem.Shared.Common.Exceptions;

public class AppException : Exception
{
    public AppException() : base()
    {
    }

    public AppException(string message) : base(message)
    {
    }

    public AppException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class ValidationException : AppException
{
    public List<string> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new List<string> { message };
    }

    public ValidationException(List<string> errors) : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}

public class NotFoundException : AppException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string name, object key) : base($"Entity \"{name}\" ({key}) was not found.")
    {
    }
}

public class UnauthorizedException : AppException
{
    public UnauthorizedException() : base("Unauthorized access.")
    {
    }

    public UnauthorizedException(string message) : base(message)
    {
    }
}

public class ForbiddenException : AppException
{
    public ForbiddenException() : base("Access forbidden.")
    {
    }

    public ForbiddenException(string message) : base(message)
    {
    }
}

public class ConflictException : AppException
{
    public ConflictException(string message) : base(message)
    {
    }
}