using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using System.Web.Http.Routing;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;
using RainWorx.FrameWorx.MVC.Areas.API.Models;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{    
    /// <summary>
    /// Provides services to manage CustomFields
    /// </summary>
    [RoutePrefix("api/CustomField")]    
    public class CustomFieldController : AuctionWorxAPIController
    {
        /// <summary>
        /// Gets a CustomField by ID
        /// </summary>
        /// <param name="id">The ID of the CustomField to get</param>
        /// <returns>An HTTP Status code of 200 (OK) and the CustomField upon success.  Otherwise, HTTP Status code 404 (Not Found) of the CustomField is not found.</returns>  
        [Route("{id}")]
        [ResponseType(typeof(CustomField))]
        public HttpResponseMessage Get(int id)
        {
            CustomField field = CommonClient.GetCustomFieldByID(id);

            if (field == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "CustomField not found");
            }
            else
            {
                return Request.CreateResponse<CustomField>(HttpStatusCode.OK, field);
            }     
        }

        /// <summary>
        /// Gets a List of CustomFields by Category ID
        /// </summary>
        /// <param name="id">The ID of the Category to get CustomFields for</param>
        /// <returns>An HTTP Status code of 200 (OK) and the List of CustomFields upon success.</returns>        
        [Route("ByCategory/{id}")]
        [ResponseType(typeof(List<CustomField>))]
        public HttpResponseMessage GetByCategoryID(int id)
        {            
            Category parent = CommonClient.GetCategoryByID(id);

            if (parent == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Parent Category not found");
            }
            else
            {
                List<CustomField> fields = CommonClient.GetFieldsByCategoryID(id);
                return Request.CreateResponse<List<CustomField>>(HttpStatusCode.OK, fields);
            }
        }

        /// <summary>
        /// Creates a new Enumeration
        /// </summary>                
        /// <param name="request">The request object containing customFieldID, name, value, and enabled</param>        
        /// <returns>An HTTP Status code of 201 (Created) upon success.</returns>
        [Route("Enumeration")]
        public HttpResponseMessage PostEnum([FromBody] CustomFieldPostEnumerationRequest request)
        {
            if (request == null) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "New Enumeration is null");
            CommonClient.AddEnumeration(Request.GetUserName(), request.customFieldID, request.name, request.name, request.value, request.enabled);
            return Request.CreateResponse(HttpStatusCode.Created);
        }

        /// <summary>
        /// Deletes an Enumeration.
        /// </summary>
        /// <param name="id">The ID of the Enumeration to delete</param>
        /// <returns>An HTTP Status code of 204 (No Content) upon success.</returns>
        [Route("Enumeration/{id}")]
        public HttpResponseMessage DeleteEnum(int id)
        {            
            CommonClient.DeleteEnum(Request.GetUserName(), id);            
            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Created a new Custom Field
        /// </summary>        
        /// <param name="customField">The Custom Field to create</param>
        /// <returns>An HTTP Status code of 201 (Created) upon success, with the Location response header set to the location of the newly created Custom Field (the GET resource).</returns>
        [Route("")]
        [ResponseType(typeof(string))]
        public HttpResponseMessage Post([FromBody] CustomField customField)
        {                        
            if (customField == null) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "CustomField is null");    
            int newid = CommonClient.AddCustomField(Request.GetUserName(), customField);            
            return Request.Created(newid);
        }

        /// <summary>
        /// Deletes a CustomField.
        /// </summary>
        /// <param name="id">The ID of the CustomField to delete</param>
        /// <returns>An HTTP Status code of 204 (No Content) upon success.</returns>
        [Route("{id}")]
        public HttpResponseMessage Delete(int id)
        {
            CommonClient.DeleteField(Request.GetUserName(), id);
            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Updates a CustomField.  Warning, do not change the Group or Type.        
        /// </summary>
        /// <param name="customField">The CustomField to update</param>
        /// <returns>An HTTP Status code of 204 (No Content) upon success.</returns>
        [Route("")]
        public HttpResponseMessage Put([FromBody]CustomField customField)
        {
            if (customField == null) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Custom Field is null");
            CommonClient.UpdateCustomField(Request.GetUserName(), customField);
            return Request.CreateResponse(HttpStatusCode.NoContent);
        }

        /// <summary>
        /// Assigns a CustomField to a Category
        /// </summary>                
        /// <param name="request">The request object containing customFieldID and categoryID</param>         
        /// <returns>An HTTP Status code of 201 (Created) upon success.</returns>
        [Route("Assign")]
        public HttpResponseMessage PostCustomFieldCategoryAssignment([FromBody] CustomFieldAssignRequest request)
        {                        
            if (request == null) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Assignment data is null");
            CommonClient.AssignFieldToCategory(Request.GetUserName(), request.customFieldID, new int[] {request.categoryID});
            return Request.CreateResponse(HttpStatusCode.Created);
        }
    }
}
