using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class CurrencyColumnSpec : ColumnSpecBase
    {
        public CurrencyColumnSpec(int number, string name, string cultureCode, string notes) : base(number, name, CustomFieldType.String, notes, false, cultureCode, string.Empty)
        {            
            Example = SiteClient.SiteCurrency;
        }

        public override bool Validate(ImportListing csvRow)
        {
            if (base.Validate(csvRow))
            {
                if (!csvRow.ColumnData.ContainsKey(Name) || string.IsNullOrEmpty(csvRow.ColumnData[Name]))
                {
                    return true;
                }
                else if (SiteClient.SupportedCurrencyRegions.Count(
                        scr => scr.Key.Equals(csvRow.ColumnData[Name], StringComparison.OrdinalIgnoreCase)) > 0)
                {
                    return true;
                }
                else
                {
                    csvRow.Disposition.Add("[" + this.Name + "] \"" + csvRow.ColumnData[Name] +
                                           "\" is not a supported currency.");
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        public override void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {            
            if (csvRow.ColumnData.ContainsKey(Name) && !string.IsNullOrEmpty(csvRow.ColumnData[Name]))
            {
                input.Add(Name, csvRow.ColumnData[Name].ToUpper());
            }
            else
            {
                input.Add(Name, SiteClient.SiteCurrency);
            }
        }
    }
}