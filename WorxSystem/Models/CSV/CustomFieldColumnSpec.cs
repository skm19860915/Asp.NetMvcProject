using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Web;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class CustomFieldColumnSpec : ColumnSpecBase
    {
        public static CustomFieldColumnSpec Create(int number, CustomField field, string cultureCode, string notes, string example)
        {
            return new CustomFieldColumnSpec(number, field.Name, notes, field.Type, false, cultureCode, example);
        }

        public CustomFieldColumnSpec(int number, string name, string notes, CustomFieldType dataType, bool required, string cultureCode, string example) 
            :base(number, name, dataType, notes, required, cultureCode, example)
        {
            
        }        
    }
}