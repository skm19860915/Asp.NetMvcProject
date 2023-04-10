using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class ShippingColumnSpec : ColumnSpecBase
    {
        public ShippingColumnSpec(int number, string name, string cultureCode, string notes, string example) : base(number, name, CustomFieldType.String, notes, false, cultureCode, example)
        {
        }

        public override bool Validate(ImportListing csvRow)
        {
            if (base.Validate(csvRow))
            {
                if (!string.IsNullOrEmpty(csvRow.ColumnData[Name]))
                {
                    //validate all pairs
                    string[] shippingOptions = csvRow.ColumnData[Name].Split('|');
                    foreach (string option in shippingOptions)
                    {
                        string[] optionSpec = option.Split(':');
                        if (optionSpec.Length != 2 && optionSpec.Length != 3)
                        {
                            csvRow.Disposition.Add("[" + this.Name + "] \"" + option + "\" within \"" +
                                                   csvRow.ColumnData[Name] +
                                                   "\" is not in the proper format \"{Shipping Method Id}:{Price}[:{Additional Price}]\" (where Shipping Method Id is an integer, followed by a colon \":\", followed by Price which is a decimal, optionally followed by a colon \":\", followed by Additional Price which is a decimal).");
                            return false;
                        }

                        int shippingMethodID;
                        if (
                            !int.TryParse(optionSpec[0], NumberStyles.Integer, CultureInfo.GetCultureInfo(CultureCode),
                                          out shippingMethodID))
                        {
                            csvRow.Disposition.Add("[" + this.Name + "] \"" + option + "\" within \"" +
                                                   csvRow.ColumnData[Name] +
                                                   "\" cannot be converted to an integer (using culture " + CultureCode +
                                                   ".");
                            return false;
                        }

                        decimal shippingMethodAmount;
                        if (
                            !decimal.TryParse(optionSpec[1], NumberStyles.Float, CultureInfo.GetCultureInfo(CultureCode),
                                              out shippingMethodAmount))
                        {
                            csvRow.Disposition.Add("[" + this.Name + "] \"" + option + "\" within \"" +
                                                   csvRow.ColumnData[Name] +
                                                   "\" cannot be converted to a decimal (using culture " + CultureCode +
                                                   ".");
                            return false;
                        }

                        if (optionSpec.Length == 3)
                        {
                            decimal shippingMethodAdditionalAmount;
                            if (
                                !decimal.TryParse(optionSpec[2], NumberStyles.Float,
                                    CultureInfo.GetCultureInfo(CultureCode),
                                    out shippingMethodAdditionalAmount))
                            {
                                csvRow.Disposition.Add("[" + this.Name + "] \"" + option + "\" within \"" +
                                                       csvRow.ColumnData[Name] +
                                                       "\" cannot be converted to a decimal (using culture " +
                                                       CultureCode +
                                                       ".");
                                return false;
                            }
                        }
                    }
                }
                return true;
            } else
            {
                return false;
            }
        }

        public override void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {
            if (!string.IsNullOrEmpty(csvRow.ColumnData[Name]))
            {
                string[] shippingOptions = csvRow.ColumnData[Name].Split('|');
                foreach (string option in shippingOptions)
                {
                    string[] optionSpec = option.Split(':');
                    int shippingMethodID = int.Parse(optionSpec[0], NumberStyles.Number,
                                                     CultureInfo.GetCultureInfo(CultureCode));
                    decimal shippingMethodAmount = decimal.Parse(optionSpec[1], NumberStyles.Float,
                                                                 CultureInfo.GetCultureInfo(CultureCode));

                    input.Add("ship_method_" + shippingMethodID, shippingMethodID.ToString());
                    input.Add("ship_amount_" + shippingMethodID, optionSpec[1]);

                    if (optionSpec.Length == 3)
                    {
                        decimal shippingMethodAdditionalAmount = decimal.Parse(optionSpec[2], NumberStyles.Float,
                                                                 CultureInfo.GetCultureInfo(CultureCode));
                        input.Add("ship_additional_" + shippingMethodID, optionSpec[2]);
                    }
                }
            }
        }
    }
}