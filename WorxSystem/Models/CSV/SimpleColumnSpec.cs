using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class SimpleColumnSpec : ColumnSpecBase
    {
        public SimpleColumnSpec(int number, string name, CustomFieldType dataType, string notes, bool required, string cultureCode, string example) : base(number, name, dataType, notes, required, cultureCode, example)
        {
        }
    }
}