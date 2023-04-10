using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public class UserColumnSpec : ColumnSpecBase
    {
        private string ActingUserName;

        public UserColumnSpec(int number, string name, string notes, string cultureCode, string actingUserName)
            : base(number, name, CustomFieldType.String, notes, false, cultureCode, string.Empty)
        {
            ActingUserName = actingUserName;
            Example = UserClient.GetUserByID(actingUserName, 1).UserName;
        }

        public override bool Validate(ImportListing csvRow)
        {
            if (base.Validate(csvRow))
            {
                string sellerUserName = ActingUserName;
                if (csvRow.ColumnData.ContainsKey(Name) && !string.IsNullOrEmpty(csvRow.ColumnData[Name]))
                {
                    sellerUserName = csvRow.ColumnData[Name];
                }
                User seller = UserClient.GetUserByUserName(ActingUserName, sellerUserName);
                if (seller == null)
                {
                    csvRow.Disposition.Add("[" + this.Name + "] \"" + csvRow.ColumnData[Name] +
                                              "\" is not a user.");
                    return false;
                }
                else if (seller.Roles.Count(r => r.Name == Strings.Roles.Admin || r.Name == Strings.Roles.Seller) <= 0)
                {
                    csvRow.Disposition.Add("[" + this.Name + "] \"" + csvRow.ColumnData[Name] +
                                              "\" is not a seller or admin user.");
                    return false;
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
            if (csvRow.ColumnData.ContainsKey(Name) && !string.IsNullOrEmpty(csvRow.ColumnData[Name]))
            {
                input.Add(Name, csvRow.ColumnData[Name]);
            }
            else
            {
                input.Add(Name, ActingUserName);
            }
        }
    }
}