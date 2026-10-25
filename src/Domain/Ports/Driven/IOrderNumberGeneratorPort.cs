namespace Domain.Ports.Driven;

public interface IOrderNumberGeneratorPort
{
    /// <summary>Takes the next order number, <c>{prefix}-{year}-{000000}</c>, from the counter of the configured
    /// prefix and that year. Call it inside <see cref="IUnitOfWorkPort.InTransaction{T}"/> together with the
    /// insert: the counter row stays locked until commit, and a rollback gives the number back (no gaps).</summary>
    Task<string> Next(int year);

    /// <summary>The number the next order would get with this prefix and year, without taking it.</summary>
    Task<string> Peek(string prefix, int year);
}
