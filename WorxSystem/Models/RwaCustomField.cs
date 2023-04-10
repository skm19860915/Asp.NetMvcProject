using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models
{
    public class RwaCustomField
    {
        public int FieldID { get; set; }
        public string FieldSymbol { get; set; }
        public string FieldTitle { get; set; }
        public int DisplaySequence { get; set; }
        public CustomFieldType AweFieldType { get; set; }
    }
}