using System.Data;
using System.Data.Common;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace FinanceTracker.Worker.Services;

/// <summary>
/// <see cref="IRunLock"/> backed by SQL Server's <c>sp_getapplock</c>.
///
/// The lock is session-scoped, so acquire and release must happen on the same open
/// connection — hence the held reference. The connection belongs to the DbContext and is
/// closed when that is disposed; this type deliberately does not close it, because doing so
/// would drop the lock early and break the SaveChangesAsync calls made during the run.
/// </summary>
public sealed class SqlServerRunLock : IRunLock
{
    private const string RunLockResource = "FinanceTracker:TransactionGenerationService:Run";

    private readonly FinanceTrackerContext _context;
    private DbConnection? _connection;

    public SqlServerRunLock(FinanceTrackerContext context) => _context = context;

    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        _connection = _context.Database.GetDbConnection();

        if (_connection.State != ConnectionState.Open)
            await _connection.OpenAsync(cancellationToken);

        using var command = _connection.CreateCommand();
        command.CommandText = "sp_getapplock";
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add(new SqlParameter("@Resource", RunLockResource));
        command.Parameters.Add(new SqlParameter("@LockMode", "Exclusive"));
        command.Parameters.Add(new SqlParameter("@LockOwner", "Session"));
        command.Parameters.Add(new SqlParameter("@LockTimeout", 0));
        var returnValue = new SqlParameter { Direction = ParameterDirection.ReturnValue };
        command.Parameters.Add(returnValue);

        await ((SqlCommand)command).ExecuteNonQueryAsync(cancellationToken);

        return (int)returnValue.Value! >= 0;
    }

    public async Task ReleaseAsync()
    {
        if (_connection is null)
            return;

        using var command = _connection.CreateCommand();
        command.CommandText = "sp_releaseapplock";
        command.CommandType = CommandType.StoredProcedure;
        command.Parameters.Add(new SqlParameter("@Resource", RunLockResource));
        command.Parameters.Add(new SqlParameter("@LockOwner", "Session"));

        await ((SqlCommand)command).ExecuteNonQueryAsync();
    }
}
