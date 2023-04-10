using System.Web.Mvc;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers.Test 
{
    public class TestController : TestControllerBase
    {
        public ActionResult Index()
        {
            return View();
        }
    }
}
