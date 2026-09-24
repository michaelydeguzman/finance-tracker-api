using System.Data;
using System.Data.Common;
using FinanceTracker.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FinanceTracker.Worker.Services;

/// <summary>
/// <see cref="IRunLock"/> backed by SQL Server's <c>sp_getapplock</c>.
///
/// The lock is session-scoped, so acquire and release must happen on the same open
/// connection — hence the held reference. The connection belongs to the DbContext and is
/// closed when that is disposed; this type deliberately does not close it, because doing so
/// would drop the lock early and break the SaveChangesAsync calls made during the run.
///
/// A session can still end underneath it: a dropped connection takes the lock with it, and
/// EF reconnects on a new session that does not hold it. <see cref="IsHeldAsync"/> is how a
/// run finds that out.
/// </summary>
public sealed class SqlServerRunLock : IRunLock
{
    private const string RunLockResource = "FinanceTracker:TransactionGenerationService:Run";

    private readonly FinanceTrackerContext _context;
    private readonly ILogger<SqlServerRunLock> _logger;
    private DbConnection? _connection;

    public SqlServerRunLock(FinanceTrackerContext context, ILogger<SqlServerRunLock> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<bool> TryAcquireAsync(CancellationToken cancellationToken = default)
    {
        // Opened through the context's execution strategy, not the raw connection. This is
        // the first thing a run does, so against an auto-paused database it is the call that
        // gets refused while the database resumes — and a raw OpenAsync is not retried.
        // Opening it through the context also tells EF to leave it open between operations,
        // which the session-scoped lock depends on.
        await _context.Database.CreateExecutionStrategy().ExecuteAsync(
            _context.Database.OpenConnectionAsync, cancellationToken);

        _connection = _context.Database.GetDbConnection();

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

    public async Task<bool> IsHeldAsync(CancellationToken cancellationToken = default)
    {
        if (_connection is not { State: ConnectionState.Open })
            return false;

        try
        {
            using var command = _connection.CreateCommand();

            // 'public' is the principal sp_getapplock takes the lock under by default. A new
            // session answers NoLock, which is exactly the case this exists to catch.
            command.CommandText = "SELECT APPLOCK_MODE('public', @Resource, 'Session')";
            command.Parameters.Add(new SqlParameter("@Resource", RunLockResource));

            return await command.ExecuteScalarAsync(cancellationToken) is "Exclusive";
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            // A connection that cannot answer cannot be holding the lock either.
            _logger.LogWarning(ex, "Could not confirm the run lock is still held; treating it as lost.");
            return false;
        }
    }

    public async Task ReleaseAsync()
    {
        // Best effort. If the session ended mid-run the lock ended with it, and releasing a
        // lock this session does not hold raises an error — thrown from the caller's finally
        // block, that would replace whatever actually ended the run.
        try
        {
            if (!await IsHeldAsync())
                return;

            using var command = _connection!.CreateCommand();
            command.CommandText = "sp_releaseapplock";
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@Resource", RunLockResource));
            command.Parameters.Add(new SqlParameter("@LockOwner", "Session"));

            await ((SqlCommand)command).ExecuteNonQueryAsync();
        }
        catch (Exception ex) when (ex is SqlException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Could not release the run lock; it ends with its session.");
        }
    }
}
