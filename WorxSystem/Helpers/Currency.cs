using System;
using System.Collections.Generic;
using System.Globalization;

namespace RainWorx.FrameWorx.MVC.Helpers
{
    public class CurrencyHelper
    {
        // Lookup dictionary of CultureInfo by currency ISO code (e.g  USD, GBP, JPY)
        //private static readonly Dictionary<string, CultureInfo> CurrencyCultureInfo;

        //public static string FormatCurrency(decimal amount, string currencyISO, string culture)
        //{
        //    CultureInfo currencyCulture = CultureInfoFromCurrencyISO(currencyISO);
        //    CultureInfo numberCulture = CultureInfo.GetCultureInfo(culture);
        //    CultureInfo finalCulture = (CultureInfo) numberCulture.Clone();
        //    finalCulture.NumberFormat.CurrencySymbol = currencyCulture.NumberFormat.CurrencySymbol;
        //    return amount.ToString("C", finalCulture) + " " + currencyISO;
        //}

        //static CurrencyHelper()
        //{
        //    CurrencyCultureInfo = new Dictionary<string, CultureInfo>();

        //    // get the list of cultures. We are not interested in neutral cultures, since
        //    // currency and RegionInfo is only applicable to specific cultures
        //    CultureInfo[] _cultures = CultureInfo.GetCultures(CultureTypes.SpecificCultures);

        //    foreach (CultureInfo ci in _cultures)
        //    {
        //        // Create a RegionInfo from culture id. 
        //        // RegionInfo holds the currency ISO code
        //        var ri = new RegionInfo(ci.LCID);

        //        // multiple cultures can have the same currency code
        //        if (!CurrencyCultureInfo.ContainsKey(ri.ISOCurrencySymbol))
        //            CurrencyCultureInfo.Add(ri.ISOCurrencySymbol, ci);
        //    }
        //}

        ///// <summary>
        ///// Lookup CultureInfo by currency ISO code
        ///// </summary>
        ///// <param name="isoCode"></param>
        ///// <returns></returns>
        //public static CultureInfo CultureInfoFromCurrencyISO(string isoCode)
        //{
        //    if (CurrencyCultureInfo.ContainsKey(isoCode))
        //        return CurrencyCultureInfo[isoCode];
        //    else
        //        return null;
        //}

/*
        /// <summary>
        /// Convert currency to a string using the specified currency format
        /// </summary>
        /// <param name="amount"></param>
        /// <param name="currencyISO"></param>
        /// <returns></returns>
        public static string FormatCurrency(decimal amount, string currencyISO)
        {            
            CultureInfo c = CultureInfoFromCurrencyISO(currencyISO);
            if (c == null)
            {
                // if currency ISO code doesn't match any culture
                // create a new culture without currency symbol
                // and use the ISO code as a prefix (e.g. YEN 123,123.00)
                c = CultureInfo.CreateSpecificCulture("en-US");
                c.NumberFormat.CurrencySymbol = "";
                c.NumberFormat.CurrencyDecimalDigits = 2;
                c.NumberFormat.CurrencyDecimalSeparator = ".";
                c.NumberFormat.CurrencyGroupSeparator = ",";                

                return String.Format("{0} {1}", currencyISO, amount.ToString("C", c.NumberFormat));
            }
            else
                return amount.ToString("C", c.NumberFormat);
        }
*/
    }
}