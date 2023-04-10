using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{
    public class ListingActionPostResponse
    {
        public bool newFeesAccrued;
        public bool accepted;
        public string reason;
        //public LineItem newPurchaseLineItem;
        public APIListing listing;
    }
}