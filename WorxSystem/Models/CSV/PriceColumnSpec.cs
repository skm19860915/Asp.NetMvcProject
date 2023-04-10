using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;
using System.Globalization;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class PriceColumnSpec : ColumnSpecBase
    {
        public PriceColumnSpec(int number, string name, CustomFieldType dataType, string notes, bool required, string cultureCode, string example) : base(number, name, dataType, notes, required, cultureCode, example)
        {
        }
        public override bool Validate(ImportListing csvRow)
        {
            bool _required = Required;
            
            //price is always optional for classified ads 
            if (csvRow.ColumnData.ContainsKey("ListingType") && csvRow.ColumnData["ListingType"] == Strings.ListingTypes.Classified)
            {
                _required = false;
            }
            if (_required)
            {
                if (!csvRow.ColumnData.ContainsKey(Name))
                {
                    //key missing
                    csvRow.Disposition.Add("[" + this.Name + "] is Required and is missing.");
                    return false;
                }
                else
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
    }
}