using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.DTO.FaultContracts;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.Models;
using RainWorx.FrameWorx.MVC.Models;
using RainWorx.FrameWorx.Utility;
using System.Globalization;

//using RainWorx.FrameWorx.WebAPI.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{
    /// <summary>
    /// Provides services to Create/Update/Get/Delete Listings
    /// </summary>
    [RoutePrefix("api/event")]
    public class EventController : AuctionWorxAPIController
    {
        /// <summary>
        /// Gets an Event by ID
        /// </summary>
        /// <param name="id">The ID of the event to get</param>
        /// <returns>An HTTP Status code of 200 (OK) and the Event on success.  Otherwise, HTTP Status code 404 (Not Found) if the event is not found.</returns>
        [Route("{id}")]
        [ResponseType(typeof(APIEvent))]
        public HttpResponseMessage Get(int id)
        {
            try
            {
                string userName = Request.GetUserName();
                DTO.Event auctionEvent = EventClient.GetEventByIDWithFillLevel(userName, id, Strings.EventFillLevels.All);
                APIEvent smallEvent = APIEvent.FromDTOEvent(auctionEvent);
                Utilities.PruneEventCustomFieldsVisbility(ref smallEvent, userName);
                return Request.CreateResponse<APIEvent>(HttpStatusCode.OK, smallEvent);
            }
            catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
            {
                //let the redirect below handle the "Event doesn't exist" error, otherwise re-throw the exception
                if (iafc.Detail.Reason != ReasonCode.EventNotExist) throw iafc;
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Event not found");
            }
        }

        /// <summary>
        /// Searches for events owned by the authenticated user
        /// </summary>        
        /// <param name="filter">"drafts", "published", "closed", "archived" or "all"</param>
        /// <param name="page">The zero-based page number to retrieve</param>
        /// <param name="size">The size of a single page</param>
        /// <returns>An HTTP Status code of 200 (OK) and the list of Events</returns>
        [Route("myevents/{filter}/{page}/{size}")]
        [ResponseType(typeof(Page<APIEvent>))]
        public HttpResponseMessage GetMyEvents(string filter, int page, int size)
        {
            string userName = Request.GetUserName();

            //parse 
            var allQueryStringVales = Request.GetQueryNameValuePairs();
            DateTime? createdAfter = null;
            DateTime tempDate = DateTime.MinValue;
            if (allQueryStringVales.Any(kvp => 
                kvp.Key.Equals("createdAfter", StringComparison.OrdinalIgnoreCase) && 
                DateTime.TryParse(kvp.Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out tempDate)))
            {
                createdAfter = tempDate;
            }

            string statuses = string.Empty;
            switch (filter.ToLower())
            {
                case "drafts":
                    statuses = Strings.AuctionEventStatuses.Draft +
                        "," + Strings.AuctionEventStatuses.Publishing;
                    break;
                case "closed":
                    statuses = Strings.AuctionEventStatuses.Closed;
                    break;
                case "archived":
                    statuses = Strings.AuctionEventStatuses.Archived;
                    break;
                case "all":
                    statuses = Strings.AuctionEventStatuses.Draft +
                        "," + Strings.AuctionEventStatuses.Publishing +
                        "," + Strings.AuctionEventStatuses.Active +
                        "," + Strings.AuctionEventStatuses.Scheduled +
                        "," + Strings.AuctionEventStatuses.Preview +
                        "," + Strings.AuctionEventStatuses.Closing +
                        "," + Strings.AuctionEventStatuses.AwaitingPayment +
                        "," + Strings.AuctionEventStatuses.Closed +
                        "," + Strings.AuctionEventStatuses.Archived;
                    break;
                default: // "published", or any invalid option
                    statuses = Strings.AuctionEventStatuses.Active +
                        "," + Strings.AuctionEventStatuses.Scheduled +
                        "," + Strings.AuctionEventStatuses.Preview +
                        "," + Strings.AuctionEventStatuses.Closing +
                        "," + Strings.AuctionEventStatuses.AwaitingPayment;
                    break;
            }

            var viewSortOpt = QuerySortDefinitions.MyEventsOptions[0];
            var results = EventClient.GetEventsByOwnerAndStatusWithFillLevel(userName, userName, statuses, page, size, viewSortOpt.Sort, viewSortOpt.Descending, Strings.EventFillLevels.All, createdAfter);
            Page<APIEvent> retVal = new Page<APIEvent>(new List<APIEvent>(results.List.Count), results.PageIndex,
                results.PageSize, results.TotalItemCount, results.SortExpression);
            foreach (Event aucEvent in results.List)
            {
                APIEvent smallEvent = APIEvent.FromDTOEvent(aucEvent);
                Utilities.PruneEventCustomFieldsVisbility(ref smallEvent, userName);
                retVal.List.Add(smallEvent);
            }
            return Request.CreateResponse<Page<APIEvent>>(HttpStatusCode.OK, retVal);
        }

        /// <summary>
        /// Searches for published, non-archived events
        /// </summary>
        /// <param name="filter">"current", "preview" or "closed"</param>
        /// <param name="page">The zero-based page number to retrieve</param>
        /// <param name="size">The size of a single page</param>
        /// <returns>An HTTP Status code of 200 (OK) and the list of Events</returns>
        [Route("search/{filter}/{page}/{size}")]
        [ResponseType(typeof(Page<APIEvent>))]
        public HttpResponseMessage GetSearchEvents(string filter, int page, int size)
        {
            //string userName = Request.GetUserName();
            string userName = string.Empty; // in this context, we are intentionally limiting the results to only data that should be available to the general public

            string statuses = string.Empty;
            switch (filter.ToLower())
            {
                case "preview":
                    statuses = Strings.AuctionEventStatuses.Preview;
                    break;
                case "closed":
                    statuses = Strings.AuctionEventStatuses.Closed;
                    break;
                default: // "current" or any invalid option
                    statuses = Strings.AuctionEventStatuses.Active +
                        "," + Strings.AuctionEventStatuses.Closing;
                    break;
            }

            var viewSortOpt = QuerySortDefinitions.MyEventsOptions[0];
            var results = EventClient.GetEventsByStatusWithFillLevel(userName, statuses, page, size, viewSortOpt.Sort, viewSortOpt.Descending, Strings.EventFillLevels.All);
            Page<APIEvent> retVal = new Page<APIEvent>(new List<APIEvent>(results.List.Count), results.PageIndex,
                results.PageSize, results.TotalItemCount, results.SortExpression);
            foreach (Event aucEvent in results.List)
            {
                APIEvent smallEvent = APIEvent.FromDTOEvent(aucEvent);
                Utilities.PruneEventCustomFieldsVisbility(ref smallEvent, userName);
                retVal.List.Add(smallEvent);
            }
            return Request.CreateResponse<Page<APIEvent>>(HttpStatusCode.OK, retVal);
        }

        /// <summary>
        /// Creates an Event
        /// </summary>
        /// <param name="input">A UserInput object containing the data with which to create the Event</param>
        /// <returns>An HTTP Status code of 201 (Created) upon success, with the Location response header set to the location of the newly created Event (the GET resource).</returns>
        [Route("")]
        [ResponseType(typeof(string))]
        public HttpResponseMessage Post([FromBody] UserInput input)
        {
            if (input == null) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "UserInput is null");
            int newEventID = 0;

            //if (!input.Items.ContainsKey("AllCategories") || string.IsNullOrEmpty(input.Items["AllCategories"]))
            //{
            //    var lineages = new List<string>();
            //    int categoryID = 0;
            //    if (input.Items.ContainsKey("CategoryID") && int.TryParse(input.Items["CategoryID"], out categoryID) && categoryID > 0)
            //    {
            //        lineages.Add(CommonClient.GetCategoryPath(categoryID).Trees[categoryID].LineageString);
            //    }
            //    int regionID = 0;
            //    if (input.Items.ContainsKey("RegionID") && int.TryParse(input.Items["RegionID"], out regionID) && regionID > 0)
            //    {
            //        lineages.Add(CommonClient.GetCategoryPath(regionID).Trees[regionID].LineageString);
            //    }
            //    if (lineages.Count > 0)
            //    {
            //        string categories = Hierarchy<int, Category>.MergeLineageStrings(lineages);
            //        input.Items.Add(Strings.Fields.AllCategories, categories);
            //    }
            //}

            if (SiteClient.EnableEvents)
            {
                newEventID = EventClient.CreateEvent(Request.GetUserName(), input);
            }
            return Request.Created(newEventID);
        }

        /// <summary>
        /// Deletes an Event.
        /// </summary>
        /// <param name="id">The ID of the Event to delete</param>
        /// <returns>An HTTP Status code of 204 (No Content) upon success.</returns>
        [Route("{id}")]
        public HttpResponseMessage Delete(int id)
        {
            try
            {
                //ListingClient.DeleteListing(Request.GetUserName(), id);
                EventClient.DeleteEvent(Request.GetUserName(), id);
            }
            catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
            {
                if (iafc.Detail.Reason != ReasonCode.EventNotExist) throw iafc;
            }
            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Updates an Event
        /// </summary>
        /// <param name="input">A UserInput object containing the data with which to update the Event</param>
        /// <returns>An HTTP Status code of 204 (No Content) upon success.</returns>
        [Route("")]
        public HttpResponseMessage Put([FromBody] UserInput input)
        {
            string username = Request.GetUserName();

            int eventId = 0;
            if (input.Items.ContainsKey(Strings.Fields.Id) && int.TryParse(input.Items[Strings.Fields.Id], out eventId))
            {
                //event ID specified properly
                //DTO.Event auctionEvent;
                //try
                //{
                //    auctionEvent = EventClient.GetEventByIDWithFillLevel(username, eventId, Strings.EventFillLevels.All);
                //}
                //catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                //{
                //    //let the redirect below handle the "Event doesn't exist" error, otherwise re-throw the exception
                //    if (iafc.Detail.Reason != ReasonCode.EventNotExist) throw iafc;
                //    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Event not found");
                //}

                EventClient.UpdateEvent(username, input, false);
                return Request.CreateResponse(HttpStatusCode.NoContent);
            }
            else
            {
                //Event ID missing or not specified properly
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                                              new ErrorResponse()
                                                  {
                                                      Message =
                                                          "Id missing from UserInput or not convertable to an int."
                                                  });
            }
        }

        ///// <summary>
        ///// Retrieves all registered listing types
        ///// </summary>
        ///// <returns>a list of listing types</returns>
        //[Route("types")]
        //[ResponseType(typeof(List<ListingType>))]
        //public HttpResponseMessage GetListingTypes()
        //{
        //    return Request.CreateResponse(HttpStatusCode.OK, ListingClient.ListingTypes);
        //}

        ///// <summary>
        ///// Retrieves all properties for a provided listing type name
        ///// </summary>        
        ///// <param name="name">The name of the listing type to get properties for</param>
        ///// <returns>A list of CustomProperties for the specified listing type</returns>
        //[Route("type/{name}")]
        //[ResponseType(typeof(List<CustomProperty>))]
        //public HttpResponseMessage GetListingTypeProperties(string name)
        //{
        //    return Request.CreateResponse(HttpStatusCode.OK, ListingClient.GetListingTypeProperties(name, "Site"));
        //}
    }
}
