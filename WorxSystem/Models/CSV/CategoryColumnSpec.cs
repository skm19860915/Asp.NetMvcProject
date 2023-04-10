using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class CategoryColumnSpec : ColumnSpecBase
    {        
        public CategoryColumnSpec(int number, string name, string notes, string cultureCode)
            : base(number, name, CustomFieldType.Int, notes, true, cultureCode, string.Empty)
        {
            List<Category> categories = CommonClient.GetChildCategories(9);
            if (categories.Count > 0)
            {
                Example = categories[0].ID.ToString();
            }    
        }

        public override bool Validate(ImportListing csvRow)
        {
            if (base.Validate(csvRow))
            {
                int categoryID = int.Parse(csvRow.ColumnData[Name]);

                Category category = CommonClient.GetCategoryByID(categoryID);

                if (category != null)
                {
                    if (category.Type == "Item")
                    {
                        return true;   
                    } else
                    {
                        csvRow.Disposition.Add("[" + this.Name + "] \"" + csvRow.ColumnData[Name] +
                                               "\" is not a Category ID.");
                        return false;    
                    }                    
                } else
                {
                    csvRow.Disposition.Add("[" + this.Name + "] \"" + csvRow.ColumnData[Name] +
                                           "\" is not a Category ID.");
                    return false;
                }                  
            }
            else
            {
                return false;
            }
        }

        public override void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {
            int CategoryID = int.Parse(csvRow.ColumnData[this.Name]);
            input.Add("CategoryID", CategoryID.ToString());
            input.Add("AllCategories", CommonClient.GetCategoryPath(CategoryID).Trees[CategoryID].LineageString);            
        }
    }
}