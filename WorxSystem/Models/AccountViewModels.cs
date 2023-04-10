using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RainWorx.FrameWorx.MVC.Models
{
    public class ExternalLoginConfirmationViewModel
    {
        public string Email { get; set; }
    }

    public class ExternalLoginListViewModel
    {
        public string ReturnUrl { get; set; }
    }
}