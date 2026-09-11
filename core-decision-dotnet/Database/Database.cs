using System.Data;
using System.Data.SQLite;
using Dapper;

namespace Photon.JobSeeker
{
    public class Database : IDisposable
    {
    private readonly SQLiteConnection connection;
    private SQLiteTransaction? transaction;

    private TrendBusiness? trend_business;
    private JobBusiness? job_business;
    private AgencyBusiness? agency_business;
    private JobOptionBusiness? job_option_business;

    static Database() => SqliteTypeHandlers.Register();

    public Database(SQLiteConnection connection)
    {
        this.connection = connection;
    }

    public bool InTransaction => transaction != null;

    public void BeginTransaction()
    {
        transaction = connection.BeginTransaction();
    }

    public void BeginTransaction(bool immediate)
    {
        transaction = immediate
            ? connection.BeginTransaction(IsolationLevel.Serializable)
            : connection.BeginTransaction(IsolationLevel.ReadCommitted);
    }

        public void Commit()
        {
            transaction?.Commit();
            transaction?.Dispose();
            transaction = null;
        }

        public void Rollback()
        {
            transaction?.Rollback();
            transaction?.Dispose();
            transaction = null;
        }

        public long LastInsertRowId()
        {
            return connection.ExecuteScalar<long>("SELECT last_insert_rowid()", transaction: transaction);
        }

        public long Changes()
        {
            return connection.ExecuteScalar<long>("SELECT changes()", transaction: transaction);
        }

        public int Execute(string command, object? param = null)
        {
            return connection.Execute(command, param, transaction: transaction);
        }

        public IEnumerable<T> Query<T>(string query, object? param = null)
        {
            return connection.Query<T>(query, param, transaction: transaction);
        }

        public IEnumerable<TReturn> Query<TFirst, TSecond, TThird, TReturn>(string query,
            Func<TFirst, TSecond, TThird, TReturn> map, object? param = null, string splitOn = "Id")
        {
            return connection.Query(query, map, param, transaction: transaction, splitOn: splitOn);
        }

        public T? ExecuteScalar<T>(string query, object? param = null)
        {
            return connection.ExecuteScalar<T>(query, param, transaction: transaction);
        }

        public List<Dictionary<string, object>> ReadAll(string query)
        {
            var list = new List<Dictionary<string, object>>();

            foreach (var row in connection.Query(query, transaction: transaction))
            {
                var record = new Dictionary<string, object>((IDictionary<string, object>)row);
                list.Add(record);
            }

            return list;
        }

        public void Dispose()
        {
            transaction?.Dispose();
            connection.Dispose();
            GC.SuppressFinalize(this);
        }

        internal TrendBusiness Trend => trend_business ??= new TrendBusiness(this);

        internal JobBusiness Job => job_business ??= new JobBusiness(this);

        internal AgencyBusiness Agency => agency_business ??= new AgencyBusiness(this);

        internal JobOptionBusiness JobOption => job_option_business ??= new JobOptionBusiness(this);
    }
}
