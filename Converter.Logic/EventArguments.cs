using System;
using Converter.Logic.Schema;

namespace Converter.Logic
{
    /// <summary>
    /// Represents a progress changed notification when processing TableSchemas.
    /// </summary>
    public class TableSchemaReaderProgressChangedEventArgs : EventArgs
    {
        public TableSchema LastProcessedTable { get; private set; }
        public int TablesProcessed { get; private set; }
        public int TablesRemaining { get; private set; }

        public TableSchemaReaderProgressChangedEventArgs() { }

        public TableSchemaReaderProgressChangedEventArgs(TableSchema lastProcessedTable, int tablesProcessed, int tablesRemaining)
        {
            LastProcessedTable = lastProcessedTable;
            TablesProcessed = tablesProcessed;
            TablesRemaining = tablesRemaining;
        }
    }

    /// <summary>
    /// Represents a progress changed notification when processing ViewSchemas.
    /// </summary>
    public class ViewSchemaReaderProgressChangedEventArgs : EventArgs
    {
        public ViewSchema LastProcessedView { get; private set; }
        public int ViewsProcessed { get; private set; }
        public int ViewsRemaining { get; private set; }

        public ViewSchemaReaderProgressChangedEventArgs() { }

        public ViewSchemaReaderProgressChangedEventArgs(ViewSchema lastProcessedView, int viewsProcessed, int viewsRemaining)
        {
            LastProcessedView = lastProcessedView;
            ViewsProcessed = viewsProcessed;
            ViewsRemaining = viewsRemaining;
        }
    }
}
