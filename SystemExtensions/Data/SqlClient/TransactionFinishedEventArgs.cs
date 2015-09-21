namespace System.Data.SqlClient
{
    public class TransactionFinishedEventArgs : EventArgs
    {
        public TransactionFinishedEventArgs(Exception error, bool committed)
        {
            Error = error;
            Committed = committed;
        }

        public Exception Error { get; private set; }
        public bool Committed { get; private set; }
    }
}