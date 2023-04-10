using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class MakeOfferColumnSpec : ColumnSpecBase
    {
        public MakeOfferColumnSpec(int number, string name, CustomFieldType dataType, string notes, bool required, string cultureCode, string example) : base(number, name, dataType, notes, required, cultureCode, example)
        {
        }

        public override void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {
            if (csvRow.ColumnData.ContainsKey(Name))
            {
                if (string.IsNullOrEmpty(csvRow.ColumnData[Name]))
                {
                    input.Add(Strings.Fields.MakeOfferAllowed, "False");
                }
                else
                {
                    input.Add(Strings.Fields.MakeOfferAllowed, csvRow.ColumnData[Name]);
                }
            }
        }
    }
}