using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Description;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Base;
using RainWorx.FrameWorx.MVC.Areas.API.Controllers.Helpers;

namespace RainWorx.FrameWorx.MVC.Areas.API.Controllers
{    
    /// <summary>
    /// Provides services to manage Categories
    /// </summary>
    [RoutePrefix("api/Category")]
    public class CategoryController : AuctionWorxAPIController
    {                      
        /// <summary>
        /// Gets the root Category
        /// </summary>
        /// <returns>An HTTP Status code of 200 (OK) and the root Category upon success.</returns>     
        [Route("")]
        [ResponseType(typeof(Category))]
        public HttpResponseMessage GetRoot()
        {
            Category result = CommonClient.GetCategoryByID(50);
            return Request.CreateResponse<Category>(HttpStatusCode.OK, result);
        }

        /// <summary>
        /// Gets immediate child categories for a specified parent category
        /// </summary>
        /// <param name="id">The ID of the parent Category to get immediate children for</param>
        /// <returns>An HTTP Status code of 200 (OK) and the List of Category upon success.</returns>                         
        [Route("Children/{id}")]
        [ResponseType(typeof(List<Category>))]
        public HttpResponseMessage GetChildren(int id)
        {
            Category parent = CommonClient.GetCategoryByID(id);

            if (parent == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Parent Category not found");
            }
            else
            {
                List<Category> categories = CommonClient.GetChildCategories(id);                
                return Request.CreateResponse<List<Category>>(HttpStatusCode.OK, categories);                
            }             
        }

        /// <summary>
        /// Gets a Category by ID
        /// </summary>
        /// <param name="id">The ID of the Category to get</param>
        /// <returns>An HTTP Status code of 200 (OK) and the Category upon success.  Otherwise, HTTP Status code 404 (Not Found) of the Category is not found.</returns>        
        [Route("{id}")]
        [ResponseType(typeof(Category))]
        public HttpResponseMessage Get(int id)
        {
            Category result = CommonClient.GetCategoryByID(id);

            if (result == null)
            {
                return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Category not found");
            }
            else
            {
                return Request.CreateResponse<Category>(HttpStatusCode.OK, result);
            } 
        }

        /// <summary>
        /// Creates a Category and adds it as a child to an existing Category.
        /// </summary>
        /// <param name="category">The Category to add.  The following properties must be populated:
        /// .Name, .ParentCategoryID, .Type oneof('Event', 'Item', 'Region', 'Site', 'Store', 'User'), .MVCAction (even if empty string)</param>       
        /// <returns>An HTTP Status code of 201 (Created) upon success, with the Location response header set to the location of the newly created Category (the GET resource).</returns>
        [Route("")]
        [ResponseType(typeof(string))]
        public HttpResponseMessage Post([FromBody]Category category)
        {
            if (category == null) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Category is null");                        
            CommonClient.AddChildCategory(Request.GetUserName(), category);            
            return Request.Created(category.ID);
        }

        /// <summary>
        /// Updates a Category.  Warning, do not change the Parent Category ID to re-root the Category.  Doing so without regenerating nested sets will cause hierarchical errors within your installation.        
        /// </summary>
        /// <param name="category">The Category to update</param>
        /// <returns>An HTTP Status code of 204 (No Content) upon success.</returns>
        [Route("")]
        public HttpResponseMessage Put([FromBody]Category category)
        {
            if (category == null) return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Category is null");
            CommonClient.UpdateCategory(Request.GetUserName(), category);
            return Request.CreateResponse(HttpStatusCode.NoContent);
        }
        
        /// <summary>
        /// Deletes a Category.  Warning, deleting a Category will delete the Category and all descendant Categories, and all listings that may be assigned to them.       
        /// </summary>
        /// <param name="id">The ID of the Category to delete</param>
        /// <returns>An HTTP Status code of 204 (No Content) upon success.</returns>
        [Route("{id}")]
        public HttpResponseMessage Delete(int id)
        {
            CommonClient.DeleteCategory(Request.GetUserName(), id);
            return Request.CreateResponse(HttpStatusCode.NoContent);
        }
    }
}
