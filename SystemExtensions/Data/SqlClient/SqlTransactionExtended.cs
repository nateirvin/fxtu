namespace System.Data.SqlClient
{
    public class SqlTransactionExtended : IDbTransaction
    {
        private readonly SqlTransaction _baseObject;

        public SqlTransactionExtended(SqlTransaction transaction)
        {
            _baseObject = transaction;
        }

        public event EventHandler<TransactionFinishedEventArgs> Finished;

        public void Dispose()
        {
            _baseObject.Dispose();
        }

        public void Commit()
        {
            ActAndNotify(() =>
            {
                _baseObject.Commit();
                return true;
            });
        }

        public void Rollback()
        {
            ActAndNotify(() =>
            {
                _baseObject.Rollback();
                return false;
            });
        }

        private void ActAndNotify(Func<bool> action)
        {
            bool success = false;
            Exception errorToReport = null;

            try
            {
                success = action();
            }
            catch (Exception caughtError)
            {
                errorToReport = caughtError;
                throw;
            }
            finally
            {
                if (Finished != null)
                {
                    Finished(this, new TransactionFinishedEventArgs(errorToReport, success));
                }
            }
        }

        public IDbConnection Connection
        {
            get { return _baseObject.Connection; }
        }

        public IsolationLevel IsolationLevel
        {
            get { return _baseObject.IsolationLevel; }
        }

        public static implicit operator SqlTransaction(SqlTransactionExtended source)
        {
            return source._baseObject;
        }
    }
}