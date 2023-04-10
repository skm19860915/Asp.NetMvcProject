using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class RegionColumnSpec : ColumnSpecBase
    {
        public RegionColumnSpec(int number, string name, string notes, bool required, string cultureCode) : base(number, name, CustomFieldType.Int, notes, required, cultureCode, string.Empty)
        {
            List<Category> regions = CommonClient.GetChildCategories(27);
            if (regions.Count > 0)
            {
                Example = regions[0].ID.ToString();   
            }            
        }

        public override bool Validate(ImportListing csvRow)
        {
            if (base.Validate(csvRow))
            {
                if (!string.IsNullOrEmpty(csvRow.ColumnData[Name]))
                {
                    int categoryID = int.Parse(csvRow.ColumnData[Name]);

                    Category category = CommonClient.GetCategoryByID(categoryID);

                    if (category != null)
                    {
                        if (category.Type == "Region")
                        {
                            return true;
                        }
                        else
                        {
                            csvRow.Disposition.Add("[" + this.Name + "] \"" + csvRow.ColumnData[Name] +
                                                   "\" is not a Region ID.");
                            return false;
                        }
                    }
                    else
                    {
                        csvRow.Disposition.Add("[" + this.Name + "] \"" + csvRow.ColumnData[Name] +
                                               "\" is not a Region ID.");
                        return false;
                    }
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        public override void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent)
        {
            if (!string.IsNullOrEmpty(csvRow.ColumnData[Name]))
            {
                int RegionID = int.Parse(csvRow.ColumnData[this.Name]);                
                input["AllCategories"] += CommonClient.GetCategoryPath(RegionID).Trees[RegionID].LineageString.Substring(2);
            }
        }
    }
}