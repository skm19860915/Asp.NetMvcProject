using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class ListingTypeColumnSpec : ColumnSpecBase
    {
        public ListingTypeColumnSpec(int number, string name, string cultureCode, string notes) : base(number, name, CustomFieldType.String, notes, false, cultureCode, string.Empty)
        {

            if (ListingClient.ListingTypes.Any(lt => lt.Name == Strings.ListingTypes.Auction))
            {
                Example = Strings.ListingTypes.Auction;
            }
            else if (ListingClient.ListingTypes.Any(lt => lt.Name == Strings.ListingTypes.FixedPrice))
            {
                Example = Strings.ListingTypes.FixedPrice;
            }
            else
            {
                Example = ListingClient.ListingTypes[0].Name;
            }
        }

        public override bool Validate(ImportListing csvRow)
        {
            if (base.Validate(csvRow))
            {
                int categoryID = int.Parse(csvRow.ColumnData["Category"]);
                List<ListingType> listingTypes = ListingClient.GetValidListingTypesForCategory(categoryID);

                if (!csvRow.ColumnData.ContainsKey(Name) || string.IsNullOrEmpty(csvRow.ColumnData[Name]))
                {
                    return (listingTypes.Count > 0);
                }
                else if (listingTypes.Count(lt => lt.Name == csvRow.ColumnData[Name]) > 0)
                {
                    return true;
                }
                else
                {
                    csvRow.Disposition.Add("[" + this.Name + "] \"" + csvRow.ColumnData[Name] +
                                           "\" is not a supported listing type in general, not supported for the specified Category, or the specified Category is not a leaf-level Category.");
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
            if (csvRow.ColumnData.ContainsKey(Name) && !string.IsNullOrEmpty(csvRow.ColumnData[Name]))
            {
                if (DataType == CustomFieldType.Boolean)
                {
                    if (string.IsNullOrEmpty(csvRow.ColumnData[Name])) input.Add(Name, "False");
                }
                else
                {
                    input.Add(Name, csvRow.ColumnData[Name]);
                }
            }
            else
            {
                //this value is missing or blank, so set to the first elligible type, prioritizing "Auction", then "FixedPrice", otherwise the first enabled type found
                int categoryID = int.Parse(csvRow.ColumnData["Category"]);
                List<ListingType> listingTypes = ListingClient.GetValidListingTypesForCategory(categoryID);
                if (listingTypes.Any(lt => lt.Name == Strings.ListingTypes.Auction))
                {
                    input.Add(Name, Strings.ListingTypes.Auction);
                }
                else if (listingTypes.Any(lt => lt.Name == Strings.ListingTypes.FixedPrice))
                {
                    input.Add(Name, Strings.ListingTypes.FixedPrice);
                }
                else
                {
                    input.Add(Name, listingTypes[0].Name);
                }
            }
        }

    }
}