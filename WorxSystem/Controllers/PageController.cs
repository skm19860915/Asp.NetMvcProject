using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.MVC.Helpers;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Controllers
{
    /// <summary>
    /// Provides methods that display custom CMS content
    /// </summary>
    [GoUnsecure]
    public class PageController : AuctionWorxController
    {
        /// <summary>
        /// Displays the specified custom CMS content
        /// </summary>
        /// <param name="name">the name of the specified custom CMS content</param>
        /// <returns>404 error if the specified content doesn't exist</returns>
        [Authenticate]
        public ActionResult Index(string name)
        {
            string culture = this.GetCookie(Strings.MVC.CultureCookie) ??
                             SiteClient.Settings[Strings.SiteProperties.SiteCulture];
            Content content = SiteClient.GetContentContainer(name, culture);
            if (content == null) return HttpNotFound();
            return View(content);
        }
    }
}
