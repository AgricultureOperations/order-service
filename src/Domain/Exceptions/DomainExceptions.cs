namespace Domain.Exceptions;

/// <summary>Input breaks a business rule. Mapped to 400.</summary>
public class DomainValidationException : Exception
{
    public DomainValidationException(string message) : base(message) { }
}

/// <summary>The operation is not allowed in the current state (e.g. an invalid status transition). Mapped to 409.</summary>
public class DomainConflictException : Exception
{
    public DomainConflictException(string message) : base(message) { }
}

/// <summary>Another request already used this Idempotency-Key. The caller returns that request's order.</summary>
public class IdempotencyKeyConflictException : DomainConflictException
{
    public IdempotencyKeyConflictException() : base("A request with this Idempotency-Key is still being processed.") { }
}

/// <summary>The resource was changed by someone else since the client read it. Mapped to 409.</summary>
public class ConcurrencyConflictException : DomainConflictException
{
    public ConcurrencyConflictException()
        : base("This order was changed by someone else. Reload it and try again.") { }
}
