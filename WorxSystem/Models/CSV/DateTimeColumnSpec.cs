using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class DateTimeColumnSpec : ColumnSpecBase
    {
        private readonly string _baseName;

        public DateTimeColumnSpec(int number, string name, string notes, bool required, string cultureCode, string baseName) : base(number, name, CustomFieldType.DateTime, notes, required, cultureCode, string.Empty)
        {
            _baseName = baseName;
            //Example = DateTime.Now.AddHours(SiteClient.TimeZoneOffset).ToString(CultureInfo.GetCultureInfo(cultureCode));
            TimeZoneInfo siteTimeZone = TimeZoneInfo.FindSystemTimeZoneById(SiteClient.SiteTimeZone);
            Example = TimeZoneInfo.ConvertTime(DateTime.UtcNow, TimeZoneInfo.Utc, siteTimeZone).ToString(CultureInfo.GetCultureInfo(cultureCode));
        }

        public override void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {
            if (!string.IsNullOrEmpty(csvRow.ColumnData[Name]))
            {
                DateTime temp = DateTime.Parse(csvRow.ColumnData[Name], CultureInfo.GetCultureInfo(CultureCode));
                DateTime newDTTM = new DateTime(temp.Year, temp.Month, temp.Day);
                input.Add(_baseName + "Date", newDTTM.ToString(CultureInfo.GetCultureInfo(CultureCode)));
                input.Add(_baseName + "Time", csvRow.ColumnData[Name]);
            }
        }
    }
}