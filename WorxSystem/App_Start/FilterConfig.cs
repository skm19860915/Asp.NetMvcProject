using System.Web;
using System.Web.Mvc;
using RainWorx.FrameWorx.MVC.Models;

namespace RainWorx.FrameWorx.MVC
{
    public class FilterConfig
    {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
            filters.Add(new LogStats());
        }
    }
}
