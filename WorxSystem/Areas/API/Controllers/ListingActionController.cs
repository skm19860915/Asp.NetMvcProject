using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{
    [RoutePrefix("api/listingaction")]    
    public class ListingActionController : AuctionWorxAPIController
    {        
        /// <summary>
        /// Creates a ListingAction (bid, purchase, etc...)
        /// </summary>
        /// <param name="input">A UserInput object containing the data with which to create the ListingAction</param>
        /// <returns>An HTTP Status code of 201 (Created) upon success, WITHOUT the Location response header set.  The response body containing newFeesAccrued, accepted, reason, newPurchaseLineItem, and listing</returns>
        [Route("")]
        [ResponseType(typeof(ListingActionPostResponse))]
        public HttpResponseMessage Post([FromBody] UserInput input)
        {
            if (input == null) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "UserInput is null");
            
            bool accepted;
            ReasonCode reasonCode;
            LineItem newPurchaseLineItem;
            Listing listing = null;

            bool newFees = ListingClient.SubmitListingAction(Request.GetUserName(), input, out accepted, out reasonCode,
                                              out newPurchaseLineItem, listing);

            ListingActionPostResponse listingActionResponse = new ListingActionPostResponse
                                                                  {
                                                                      newFeesAccrued = newFees,
                                                                      accepted = accepted,
                                                                      reason = Enum.GetName(typeof(ReasonCode), reasonCode),
                                                                      //newPurchaseLineItem = newPurchaseLineItem,
                                                                      listing = APIListing.FromDTOListing(listing)
                                                                  };

            return Request.CreateResponse<ListingActionPostResponse>(HttpStatusCode.Created, listingActionResponse);  
        }
    }
}
