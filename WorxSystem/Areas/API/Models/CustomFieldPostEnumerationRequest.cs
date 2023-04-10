using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RainWorx.FrameWorx.MVC.Areas.API.Models
{
    public class CustomFieldPostEnumerationRequest
    {
        public int customFieldID;
        public string name;
        public string value;
        public bool enabled;
    }    
}