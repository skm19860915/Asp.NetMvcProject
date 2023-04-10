using System;
namespace RainWorx.FrameWorx.MVC.Models
{
    /// <summary>
    /// Stores details about a generic report column
    /// </summary>
    public class ReportColumn
    {

        /// <summary>
        /// Creates a new ReportColumn object with both input keys defined
        /// </summary>
        /// <param name="columnName">the system name of the column, required</param>
        /// <param name="displayName">the localized name of the column, required</param>
        /// <param name="inputKey1">the first filter key, optional</param>
        /// <param name="inputKey2">the second filter key, optional</param>
        /// <param name="inputType">the data type expected from the input(s)</param>
        /// <param name="isSortable">false to indicate this column is NOT sortable</param>
        /// <param name="colSpan">number of columns to spread this value over (default: 1)</param>
        public ReportColumn(string columnName, string displayName, string inputKey1, string inputKey2, string inputType, bool isSortable, int colSpan)
        {
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException("columnName");
            if (string.IsNullOrEmpty(displayName)) throw new ArgumentNullException("displayName");
            if ((!string.IsNullOrEmpty(inputKey1) || !string.IsNullOrEmpty(inputKey2)) && string.IsNullOrEmpty(inputType))
                throw new ArgumentNullException("displayName");
            ColumnName = columnName;
            DisplayName = displayName;
            InputKey1 = inputKey1;
            InputKey2 = inputKey2;
            InputType = inputType;
            IsSortable = isSortable;
            ColSpan = colSpan;
        }
        /// <summary>
        /// Creates a new ReportColumn object with both input keys defined
        /// </summary>
        /// <param name="columnName">the system name of the column, required</param>
        /// <param name="displayName">the localized name of the column, required</param>
        /// <param name="inputKey1">the first filter key, optional</param>
        /// <param name="inputKey2">the second filter key, optional</param>
        /// <param name="inputType">the data type expected from the input(s)</param>
        public ReportColumn(string columnName, string displayName, string inputKey1, string inputKey2, string inputType)
        {
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException("columnName");
            if (string.IsNullOrEmpty(displayName)) throw new ArgumentNullException("displayName");
            ColumnName = columnName;
            DisplayName = displayName;
            InputKey1 = inputKey1;
            InputKey2 = inputKey2;
            InputType = inputType;
            IsSortable = true;
            ColSpan = 1;
        }
        /// <summary>
        /// Creates a new ReportColumn object with the first input key defined
        /// </summary>
        /// <param name="columnName">the system name of the column, required</param>
        /// <param name="displayName">the name of the column, required</param>
        /// <param name="inputKey1">the first filter key, optional</param>
        /// <param name="inputType">the data type expected from the input(s)</param>
        public ReportColumn(string columnName, string displayName, string inputKey1, string inputType)
        {
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException("columnName");
            if (string.IsNullOrEmpty(displayName)) throw new ArgumentNullException("displayName");
            ColumnName = columnName;
            DisplayName = displayName;
            InputKey1 = inputKey1;
            InputKey2 = null;
            InputType = inputType;
            IsSortable = true;
            ColSpan = 1;
        }
        /// <summary>
        /// Creates a new ReportColumn object with no input keys
        /// </summary>
        /// <param name="columnName">the system name of the column, required</param>
        /// <param name="displayName">the name of the column, required</param>
        public ReportColumn(string columnName, string displayName)
        {
            if (string.IsNullOrEmpty(columnName)) throw new ArgumentNullException("columnName");
            if (string.IsNullOrEmpty(displayName)) throw new ArgumentNullException("displayName");
            ColumnName = columnName;
            DisplayName = displayName;
            InputKey1 = null;
            InputKey2 = null;
            InputType = null;
            IsSortable = true;
            ColSpan = 1;
        }

        /// <summary>
        /// the name of the column, required
        /// </summary>
        public string ColumnName { get; set; }
        /// <summary>
        /// the name of the column, required
        /// </summary>
        public string DisplayName { get; set; }
        /// <summary>
        /// the first filter key, optional
        /// </summary>
        public string InputKey1 { get; set; }
        /// <summary>
        /// the second filter key, optional
        /// </summary>
        public string InputKey2 { get; set; }
        /// <summary>
        /// the second filter key, optional
        /// </summary>
        public string InputType { get; set; }
        /// <summary>
        /// true to indicate the report can be sorted by the column (default: true)
        /// </summary>
        public bool IsSortable { get; set; }
        /// <summary>
        /// number of columns to spread this value over (default: 1)
        /// </summary>
        public int ColSpan { get; set; }
    }
}
