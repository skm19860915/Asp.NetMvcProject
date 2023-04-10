using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class ColumnHelpSpec
    {
        public int ColumnNumber { get; private set; }
        public string ColumnString { get; private set; }
        public string Name { get; private set; }
        public string DataType { get; private set; }
        public string Notes { get; private set; }
        public string Example { get; private set; }
        public bool Required { get; private set; }

        public ColumnHelpSpec(int colNum, string colStr, string name, string dataType, string notes, bool required, string example)
        {
            ColumnNumber = colNum;
            ColumnString = colStr;
            Name = name;
            DataType = dataType;
            Notes = notes;
            Required = required;
            Example = example;
        }
    }

    public abstract class ColumnSpecBase: IColumnSpec
    {
        protected int Number { get; set; }
        public string Name { get; set; }
        protected CustomFieldType DataType { get; set; }
        protected string Notes { get; set; }
        protected bool Required { get;  set; }
        protected string CultureCode { get; set; }
        protected string Example { get; set; }

        public ColumnSpecBase(int number, string name, CustomFieldType dataType, string notes, bool required, string cultureCode, string example)
        {
            Number = number;
            Name = name;
            DataType = dataType;
            Notes = notes;
            Required = required;
            CultureCode = cultureCode;
            Example = example;
        }

        public ColumnHelpSpec ToHelp()
        {
            return new ColumnHelpSpec(Number, ColumnNumberToString(), Name, DataType.ToString(), Notes, Required, Example);
        }

        public virtual bool Validate(ImportListing csvRow)
        {            
            if (Required)
            {
                if (!csvRow.ColumnData.ContainsKey(Name))
                {
                    //key missing
                    csvRow.Disposition.Add("[" + this.Name + "] is Required and is missing.");
                    return false;
                } else
                {
                    if (string.IsNullOrEmpty(csvRow.ColumnData[Name]))
                    {
                        //value missing
                        csvRow.Disposition.Add("[" + this.Name + "] is Required and is blank.");
                        return false;
                    }
                }
            }

            if (!csvRow.ColumnData.ContainsKey(Name))
            {
                //column missing, no data to validate
                return true;
            }

            //if value contains data, do a data type check
            if (!string.IsNullOrEmpty(csvRow.ColumnData[Name]))
            {
                switch (DataType)
                {
                    case CustomFieldType.Boolean:
                        bool tempBool;
                        if (!bool.TryParse(csvRow.ColumnData[Name], out tempBool))
                        {
                            //bad format for bool
                            csvRow.Disposition.Add("[" + this.Name + "] should be a bool but \"" + csvRow.ColumnData[Name] + "\" cannot be converted to one (must be \"true\" or \"false\", case-insensitive).");
                            return false;
                        }
                        break;
                    case CustomFieldType.Int:
                        int tempInt;
                        if (!int.TryParse(csvRow.ColumnData[Name], NumberStyles.Number, CultureInfo.GetCultureInfo(CultureCode), out tempInt))
                        {
                            //bad format for int
                            csvRow.Disposition.Add("[" + this.Name + "] should be an integer but \"" + csvRow.ColumnData[Name] + "\" cannot be converted to one (using culture " + CultureCode + ").");
                            return false;
                        }
                        break;
                    case CustomFieldType.DateTime:
                        DateTime tempDateTime;
                        if (!DateTime.TryParse(csvRow.ColumnData[Name], CultureInfo.GetCultureInfo(CultureCode), DateTimeStyles.None, out tempDateTime))
                        {
                            //bad format for datetime
                            csvRow.Disposition.Add("[" + this.Name + "] should be a DateTime but \"" + csvRow.ColumnData[Name] + "\" cannot be converted to one (using culture " + CultureCode + ").");
                            return false;
                        }
                        break;
                    case CustomFieldType.Decimal:
                        decimal tempDecimal;
                        if (!decimal.TryParse(csvRow.ColumnData[Name], NumberStyles.Number, CultureInfo.GetCultureInfo(CultureCode), out tempDecimal))
                        {
                            //bad format for decimal
                            csvRow.Disposition.Add("[" + this.Name + "] should be a decimal but \"" + csvRow.ColumnData[Name] + "\" cannot be converted to one (using culture " + CultureCode + ").");
                            return false;
                        }
                        break;
                    case CustomFieldType.String:
                    case CustomFieldType.Enum:
                    default:
                        //string and enum don't need type checking (they are already strings)
                        break;
                }
            }

            return true;
        }

        public virtual void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {
            if (csvRow.ColumnData.ContainsKey(Name))
            {
                if (DataType == CustomFieldType.Boolean && string.IsNullOrEmpty(csvRow.ColumnData[Name]))
                {
                    input.Add(Name, "False");
                }
                else
                {
                    input.Add(Name, csvRow.ColumnData[Name]);
                }
            }
        }

        protected string ColumnNumberToString()
        {
            int dividend = Number;
            string columnName = String.Empty;
            int modulo;

            while (dividend > 0)
            {
                modulo = (dividend - 1) % 26;
                columnName = Convert.ToChar(65 + modulo).ToString() + columnName;
                dividend = (int)((dividend - modulo) / 26);
            }

            return columnName;
        }


    }
}