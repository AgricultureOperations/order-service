namespace Domain.Ports.Driven;

public interface IUnitOfWorkPort
{
    /// <summary>Runs <paramref name="work"/> in one database transaction shared by every driven adapter of the
    /// request: it commits when the work returns and rolls back when it throws.</summary>
    Task<T> InTransaction<T>(Func<Task<T>> work);
}
