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
using RainWorx.FrameWorx.Utility;

using RainWorx.FrameWorx.MVC.Helpers;
using RainWorx.FrameWorx.Strings;

//using RainWorx.FrameWorx.WebAPI.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{
    /// <summary>
    /// Provides services to Create/Update/Get/Delete Listings
    /// </summary>
    [RoutePrefix("api/listing")]
    public class ListingController : AuctionWorxAPIController
    {
        /// <summary>
        /// Gets a listing by ID
        /// </summary>
        /// <param name="id">The ID of the listing to get</param>
        /// <returns>An HTTP Status code of 200 (OK) and the Listing on success.  Otherwise, HTTP Status code 404 (Not Found) if the Listing is not found.</returns>
        [Route("{id}")]
        [ResponseType(typeof(APIListing))]
        public HttpResponseMessage Get(int id)
        {
            try
            {
                string userName = Request.GetUserName();
                DTO.Listing listing = ListingClient.GetListingByIDWithFillLevel(userName, id, Strings.ListingFillLevels.Default);
                APIListing smallListing = APIListing.FromDTOListing(listing);
                Utilities.PruneListingCustomFieldsVisbility(ref smallListing, userName);
                return Request.CreateResponse<APIListing>(HttpStatusCode.OK, smallListing);
            }
            catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
            {
                //let the redirect below handle the "Listing doesn't exist" error, otherwise re-throw the exception
                if (iafc.Detail.Reason != ReasonCode.ListingNotExist) throw iafc;
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Listing not found");
            }
        }

        /// <summary>
        /// Searches for listings
        /// </summary>        
        /// <param name="page">The zero-based page number to retrieve</param>
        /// <param name="size">The size of a single page</param>
        /// <returns>An HTTP Status code of 200 (OK) and the Listing on success.  Otherwise, HTTP Status code 404 (Not Found) if the Listing is not found.</returns>
        [Route("search/{page}/{size}")]
        [ResponseType(typeof(Page<APIListing>))]
        public HttpResponseMessage GetSearchListings(int page, int size)
        {
            string userName = Request.GetUserName();

            ListingPageQuery query = new ListingPageQuery();
            query.Descending = true;
            query.Index = 0;
            query.Name = "Search";
            query.Input = new UserInput(userName, userName);
            query.Input.Items = Request.GetQueryNameValuePairs()
                .ToDictionary((keyItem) => keyItem.Key, (valueItem) => valueItem.Value);

            int eventID = 0;
            if (query.Input.Items.ContainsKey(Strings.Fields.EventID) && int.TryParse(query.Input.Items[Strings.Fields.EventID], out eventID))
            {
                var currentEvent = EventClient.GetEventByIDWithFillLevel(userName, eventID, Strings.EventFillLevels.None);
                if (currentEvent != null)
                {
                    //add event category id
                    query.Input.Items[Strings.Fields.EventCategoryID] = currentEvent.CategoryID.ToString();
                }
            }

            Dictionary<int, int> notUsed = null;
            Page<Listing> results = ListingClient.SearchListingsWithFillLevel(userName, query, page, size, Strings.ListingFillLevels.Default, false, out notUsed);

            User user = UserClient.GetUserByUserName(userName, userName);

            Page<APIListing> retVal = new Page<APIListing>(new List<APIListing>(results.List.Count), results.PageIndex,
                results.PageSize, results.TotalItemCount, results.SortExpression);
            foreach (Listing listing in results.List)
            {
                APIListing smallListing = APIListing.FromDTOListing(listing);
                Utilities.PruneListingCustomFieldsVisbility(ref smallListing, user);
                retVal.List.Add(smallListing);
            }

            return Request.CreateResponse<Page<APIListing>>(HttpStatusCode.OK, retVal);
        }

        /// <summary>
        /// Creates a Listing
        /// </summary>
        /// <param name="input">A UserInput object containing the data with which to create the Listing</param>
        /// <returns>An HTTP Status code of 201 (Created) upon success, with the Location response header set to the location of the newly created Listing (the GET resource).</returns>
        [Route("")]
        [ResponseType(typeof(string))]
        public HttpResponseMessage Post([FromBody] UserInput input)
        {
            if (input == null) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "UserInput is null");
            int newListingID;

            if (!input.Items.ContainsKey("AllCategories") || string.IsNullOrEmpty(input.Items["AllCategories"]))
            {
                var lineages = new List<string>();
                int categoryID = 0;
                if (input.Items.ContainsKey("CategoryID") && int.TryParse(input.Items["CategoryID"], out categoryID) && categoryID > 0)
                {
                    lineages.Add(CommonClient.GetCategoryPath(categoryID).Trees[categoryID].LineageString);
                }
                int regionID = 0;
                if (input.Items.ContainsKey("RegionID") && int.TryParse(input.Items["RegionID"], out regionID) && regionID > 0)
                {
                    lineages.Add(CommonClient.GetCategoryPath(regionID).Trees[regionID].LineageString);
                }
                if (lineages.Count > 0)
                {
                    string categories = Hierarchy<int, Category>.MergeLineageStrings(lineages);
                    input.Items.Add(Strings.Fields.AllCategories, categories);
                }
            }

            if (SiteClient.EnableEvents && input.Items.ContainsKey(Strings.Fields.EventID))
            {
                EventClient.CreateLot(Request.GetUserName(), input, false, out newListingID);
            }
            else
            {
                ListingClient.CreateListing(Request.GetUserName(), input, false, out newListingID);
            }
            return Request.Created(newListingID);
        }

        /// <summary>
        /// Deletes a Listing.
        /// </summary>
        /// <param name="id">The ID of the Listing to delete</param>
        /// <returns>An HTTP Status code of 204 (No Content) upon success.</returns>
        [Route("{id}")]
        public HttpResponseMessage Delete(int id)
        {
            DTO.Listing listing = null;
            if (SiteClient.EnableEvents)
            {
                try
                {
                    listing = ListingClient.GetListingByIDWithFillLevel(Request.GetUserName(), id, Strings.ListingFillLevels.LotEvent);
                }
                catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                {
                    //handle the "Listing doesn't exist" error, otherwise re-throw the exception
                    if (iafc.Detail.Reason != ReasonCode.ListingNotExist) throw iafc;
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Listing not found");
                }
            }

            try
            {
                if (listing != null && listing.Lot != null)
                {
                    EventClient.DeleteLot(Request.GetUserName(), listing.Lot.ID);
                }
                else
                {
                    ListingClient.DeleteListing(Request.GetUserName(), id);
                }
            }
            catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
            {
                if (iafc.Detail.Reason != ReasonCode.ListingNotExist) throw iafc;
            }
            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Updates a Listing
        /// </summary>
        /// <param name="input">A UserInput object containing the data with which to update the Listing</param>
        /// <returns>An HTTP Status code of 204 (No Content) upon success.</returns>
        [Route("")]
        public HttpResponseMessage Put([FromBody] UserInput input)
        {
            string username = Request.GetUserName();

            int listingID = 0;
            if (input.Items.ContainsKey("ListingID") && int.TryParse(input.Items["ListingID"], out listingID))
            {
                //listing ID specified properly
                DTO.Listing listing;
                try
                {
                    listing = ListingClient.GetListingByIDWithFillLevel(username, listingID, Strings.ListingFillLevels.Default);
                }
                catch (System.ServiceModel.FaultException<InvalidArgumentFaultContract> iafc)
                {
                    //let the redirect below handle the "Listing doesn't exist" error, otherwise re-throw the exception
                    if (iafc.Detail.Reason != ReasonCode.ListingNotExist) throw iafc;
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Listing not found");
                }

                var defaultInputs = new UserInput(input.ActingUserName, input.FBOUserName, input.CultureName, input.CultureUIName);
                defaultInputs.FillInputFromListing(listing);

                if (listing.Lot != null)
                {
                    defaultInputs.Items[Fields.LotNumber] = listing.Lot.LotNumber;
                    foreach(string missingDefaultKey in defaultInputs.Items.Keys.Where(dk => !input.Items.Keys.Any(k => k == dk)))
                    {
                        input.Items[missingDefaultKey] = defaultInputs.Items[missingDefaultKey];
                    }
                    EventClient.UpdateLotWithUserInput(username, listing.Lot, input);
                }
                else
                {
                    foreach (string missingDefaultKey in defaultInputs.Items.Keys.Where(dk => !input.Items.Keys.Any(k => k == dk)))
                    {
                        input.Items[missingDefaultKey] = defaultInputs.Items[missingDefaultKey];
                    }
                    ListingClient.UpdateListingWithUserInput(username, listing, input);
                }
                return Request.CreateResponse(HttpStatusCode.NoContent);
            }
            else
            {
                //listing ID missing or not specified properly
                return Request.CreateResponse(HttpStatusCode.BadRequest,
                                              new ErrorResponse()
                                                  {
                                                      Message =
                                                          "ListingID missing from UserInput or not convertable to an int."
                                                  });
            }
        }

        /// <summary>
        /// Retrieves all registered listing types
        /// </summary>
        /// <returns>a list of listing types</returns>
        [Route("types")]
        [ResponseType(typeof(List<ListingType>))]
        public HttpResponseMessage GetListingTypes()
        {
            return Request.CreateResponse(HttpStatusCode.OK, ListingClient.ListingTypes);
        }

        /// <summary>
        /// Retrieves all properties for a provided listing type name
        /// </summary>        
        /// <param name="name">The name of the listing type to get properties for</param>
        /// <returns>A list of CustomProperties for the specified listing type</returns>
        [Route("type/{name}")]
        [ResponseType(typeof(List<CustomProperty>))]
        public HttpResponseMessage GetListingTypeProperties(string name)
        {
            return Request.CreateResponse(HttpStatusCode.OK, ListingClient.GetListingTypeProperties(name, "Site"));
        }
    }
}
