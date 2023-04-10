using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.VisualBasic.FileIO;
using System.IO;
using RainWorx.FrameWorx.Clients;
using RainWorx.FrameWorx.DTO;
using RainWorx.FrameWorx.Strings;

using System.Web.Mvc;
using RainWorx.FrameWorx.MVC.Helpers;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    /// <summary>
    /// provides methods and containers for importing CSV data
    /// </summary>
    public static class CSV
    {
        /// <summary>
        /// Gets a list of CSV Import columns based on current admin settings
        /// </summary>
        /// <param name="controller">reference to the controller calling this method, to enable access to RESX resrouces</param>
        /// <param name="actingUserName">the username of the acting user</param>
        /// <param name="webroot">the physical file path of the application root</param>
        /// <param name="cultureCode">the culture code to use for displaying and interpreting all numeric and date values</param>
        /// <param name="saveAsDraft">if true, only fields required for draft listings/lots will return validation errors when missing</param>
        /// <returns></returns>
        public static List<IColumnSpec> GetColumnSpec(Controller controller, string actingUserName, string webroot, string cultureCode, bool saveAsDraft)
        {
            List<CustomProperty> auctionProperties = ListingClient.GetListingTypeProperties(ListingTypes.Auction, "Site");
            bool reserveEnabled = bool.Parse(auctionProperties.Single(ap => ap.Field.Name == SiteProperties.EnableReserve).Value); //Reserve Enabled for Auctions
            bool buyNowEnabled = bool.Parse(auctionProperties.Single(ap => ap.Field.Name == SiteProperties.EnableBuyNow).Value); //Buy Now Enabled for Auctions
            List<CustomProperty> fixedPriceProperties = ListingClient.GetListingTypeProperties(ListingTypes.FixedPrice, "Site");
            bool fixedPriceEnabled = bool.Parse(fixedPriceProperties.Single(ap => ap.Field.Name == SiteProperties.Enabled).Value); //Fixed Price Listing Type Enabled
            bool mixedModeEnabled = SiteClient.BoolSetting(SiteProperties.EnableNonAuctionListingsForEvents);

            bool makeOfferEnabled = false;
            foreach (var listingType in ListingClient.ListingTypes)
            {
                var property = ListingClient.GetListingTypeProperties(listingType.Name, "Site").FirstOrDefault(p =>
                    p.Field.Name == Strings.SiteProperties.EnableMakeOffer && !string.IsNullOrWhiteSpace(p.Value));
                if (property != null && bool.Parse(property.Value) == true)
                {
                    makeOfferEnabled = true;
                }
            }

            if (!webroot.EndsWith("\\"))
            {
                webroot += "\\";
            }

            int colIndex = 1;
            List<IColumnSpec> columnSpec = new List<IColumnSpec>();
            if (SiteClient.EnableEvents)
            {
                string eventIdNotes = controller.GlobalResourceString("CSVImport_Notes_EventID");
                bool eventIdIsRequired = true;
                if (mixedModeEnabled)
                {
                    eventIdNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_EventID_MixedMode"));
                    eventIdIsRequired = false;
                }
                columnSpec.Add(new SimpleColumnSpec(colIndex++, "EventID", CustomFieldType.Int, eventIdNotes, eventIdIsRequired, cultureCode, "123456"));
                string lotNumberNotes = controller.GlobalResourceString("CSVImport_Notes_LotNumber");
                if (mixedModeEnabled)
                {
                    lotNumberNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_LotNumber_MixedMode"));
                }
                columnSpec.Add(new SimpleColumnSpec(colIndex++, "LotNumber", CustomFieldType.String, lotNumberNotes, false, cultureCode, "12345-B"));
            }
            if (!SiteClient.EnableEvents || mixedModeEnabled)
            {
                string sellerUserNameNotes = controller.GlobalResourceString("CSVImport_Notes_SellersUsername");
                if (mixedModeEnabled)
                {
                    sellerUserNameNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_SellersUsername_MixedMode"));
                }
                columnSpec.Add(new UserColumnSpec(colIndex++, "Seller", sellerUserNameNotes, cultureCode, actingUserName));
            }
            columnSpec.Add(new CategoryColumnSpec(colIndex++, "Category", controller.GlobalResourceString("CSVImport_Notes_Category"), cultureCode));
            columnSpec.Add(new RegionColumnSpec(colIndex++, "Region", controller.GlobalResourceString("CSVImport_Notes_Region"), false, cultureCode));

            StringBuilder sb = new StringBuilder();

            if (!SiteClient.EnableEvents || mixedModeEnabled)
            {
                sb = new StringBuilder();
                sb.AppendLine("<ul>");
                foreach (var listingType in ListingClient.ListingTypes)
                {
                    if (mixedModeEnabled && listingType.Name == ListingTypes.Auction)
                        continue;

                    sb.Append("<li>");
                    sb.Append(listingType.Name);
                    sb.AppendLine("</li>");
                }
                sb.AppendLine("</ul>");
                string listingTypeNotes = controller.GlobalResourceString("CSVImport_Notes_ListingType", sb.ToString());
                if (mixedModeEnabled)
                {
                    listingTypeNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_ListingType_MixedMode"));
                }
                columnSpec.Add(new ListingTypeColumnSpec(colIndex++, "ListingType", cultureCode, listingTypeNotes));
            }

            sb = new StringBuilder();
            sb.AppendLine("<ul>");
            foreach (var currency in SiteClient.SupportedCurrencyRegions.OrderBy(ci => ci.Value.CurrencyNativeName))
            {
                sb.Append("<li>");
                sb.Append(currency.Key);
                sb.AppendLine("</li>");
            }
            sb.AppendLine("</ul>");
            string currencyNotes = controller.GlobalResourceString("CSVImport_Notes_Currency", sb.ToString());
            if (!SiteClient.EnableEvents || mixedModeEnabled)
            {
                if (mixedModeEnabled)
                {
                    currencyNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_Currency_MixedMode"));
                }
                columnSpec.Add(new CurrencyColumnSpec(colIndex++, "Currency", cultureCode, currencyNotes));
            }

            columnSpec.Add(new SimpleColumnSpec(colIndex++, "Title", CustomFieldType.String, controller.GlobalResourceString("CSVImport_Notes_Title"), true, cultureCode, controller.GlobalResourceString("CSVImport_Example_Title")));
            columnSpec.Add(new SimpleColumnSpec(colIndex++, "Subtitle", CustomFieldType.String, controller.GlobalResourceString("CSVImport_Notes_Subtitle"), false, cultureCode, controller.GlobalResourceString("CSVImport_Example_Subtitle")));
            columnSpec.Add(new SimpleColumnSpec(colIndex++, "Description", CustomFieldType.String, controller.GlobalResourceString("CSVImport_Notes_Description"), (!saveAsDraft), cultureCode, controller.GlobalResourceString("CSVImport_Example_Subtitle")));
            columnSpec.Add(new PriceColumnSpec(colIndex++, "Price", CustomFieldType.Decimal, controller.GlobalResourceString("CSVImport_Notes_Price"), (!saveAsDraft), cultureCode, 9.95M.ToString(CultureInfo.GetCultureInfo(cultureCode))));
            if (reserveEnabled)
            {
                columnSpec.Add(new SimpleColumnSpec(colIndex++, "ReservePrice", CustomFieldType.Decimal, controller.GlobalResourceString("CSVImport_Notes_ReservePrice"), false, cultureCode, 14.95M.ToString(CultureInfo.GetCultureInfo(cultureCode))));
            }
            if (buyNowEnabled)
            {
                columnSpec.Add(new SimpleColumnSpec(colIndex++, "FixedPrice", CustomFieldType.Decimal, controller.GlobalResourceString("CSVImport_Notes_FixedPrice"), false, cultureCode, 30.0M.ToString(CultureInfo.GetCultureInfo(cultureCode))));
            }
            if ((!SiteClient.EnableEvents || mixedModeEnabled) && fixedPriceEnabled)
            {
                columnSpec.Add(new SimpleColumnSpec(colIndex++, "Quantity", CustomFieldType.Int, controller.GlobalResourceString("CSVImport_Notes_Quantity"), false, cultureCode, "12"));
            }
            if (makeOfferEnabled)
            {
                string makeOfferNotes = controller.GlobalResourceString("CSVImport_Notes_AcceptOffers");
                if (mixedModeEnabled)
                {
                    makeOfferNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_AcceptOffers_MixedMode"));
                }
                columnSpec.Add(new MakeOfferColumnSpec(colIndex++, "AcceptOffers", CustomFieldType.Boolean, makeOfferNotes, false, cultureCode, "False"));
            }

            if (!SiteClient.BoolSetting(SiteProperties.HideTaxFields))
            {
                string taxableNotes = controller.GlobalResourceString("CSVImport_Notes_IsTaxable");
                columnSpec.Add(new SimpleColumnSpec(colIndex++, Fields.IsTaxable, CustomFieldType.Boolean, taxableNotes, true, cultureCode, "True"));
            }

            int numImages = int.Parse(SiteClient.Settings["MaxImagesPerItem"]);
            for (int i=1;i<=numImages;i++)
            {
                string imgNotes = controller.GlobalResourceString("CSVImport_Notes_Image", i, webroot, (webroot + @"Content\Images\Logos\AuctionWorxLogo31EN-240x40.png").Replace('\\', '/'));
                if (i == 1) imgNotes += "  " + controller.GlobalResourceString("CSVImport_Notes_Image1Addendum");
                string imgExample = controller.GlobalResourceString("CSVImport_Example_Image");
                columnSpec.Add(new ImageColumnSpec(colIndex++, "Image_" + i, cultureCode, actingUserName, webroot, i, imgNotes, imgExample));
            }
            if (SiteClient.BoolSetting(Strings.SiteProperties.EnableYoueTubeVideos))
            {
                columnSpec.Add(new YouTubeColumnSpec(colIndex++, "YouTubeID", cultureCode, actingUserName, 101, controller.GlobalResourceString("CSVImport_Notes_YouTubeID")));
            }
            if (SiteClient.BoolSetting(Strings.SiteProperties.EnablePDFAttachments))
            {
                string pdfNotes = controller.GlobalResourceString("CSVImport_Notes_PdfAttachments", webroot, (webroot + @"Content\PDF\test.pdf").Replace('\\', '/'), "{", "}");
                string pdfExample = controller.GlobalResourceString("CSVImport_Example_PdfAttachments", "{", "}");
                columnSpec.Add(new PdfColumnSpec(colIndex++, "PdfAttachments", cultureCode, actingUserName, webroot, pdfNotes, pdfExample));
            }

            if (!SiteClient.EnableEvents)
            {
                columnSpec.Add(new LocationColumnSpec(colIndex++, "Featured", controller.GlobalResourceString("CSVImport_Notes_Featured"), cultureCode, 1, "True"));
            }
            columnSpec.Add(new DecorationColumnSpec(colIndex++, "Bold", controller.GlobalResourceString("CSVImport_Notes_Bold"), cultureCode, 1, "True"));
            columnSpec.Add(new DecorationColumnSpec(colIndex++, "Badge", controller.GlobalResourceString("CSVImport_Notes_Badge"), cultureCode, 2, "False"));
            columnSpec.Add(new DecorationColumnSpec(colIndex++, "Highlight", controller.GlobalResourceString("CSVImport_Notes_Highlight"), cultureCode, 3, "False"));

            string shippingExample = string.Empty;
            string shippingNotes = string.Empty;
            List<ShippingMethod> shippingMethods = SiteClient.ShippingMethods;
            sb = new StringBuilder();
            foreach (ShippingMethod method in shippingMethods)
            {
                sb.Append("<tr><td>");
                sb.Append(method.ID);
                sb.Append("</td><td>&nbsp;&nbsp;</td><td>");
                sb.Append(method.Name);
                sb.AppendLine("</td></tr>");
            }
            shippingNotes += controller.GlobalResourceString("CSVImport_Notes_Shipping1", 
                "{Shipping Method Id}:{Price}[:{Additional Price}][ |...n ]",
                "{Price}",
                "{Additional Price}",
                sb.ToString());

            if (shippingMethods.Count > 1)
            {
                sb = new StringBuilder();
                sb.Append(shippingMethods[0].ID);
                sb.Append(":");
                sb.Append((8.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)));
                sb.Append(":");
                sb.Append((2.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)));
                sb.Append("|");
                sb.Append(shippingMethods[1].ID);
                sb.Append(":");
                sb.Append((13.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)));
                sb.Append(":");
                sb.Append((2.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)));
                shippingNotes += controller.GlobalResourceString("CSVImport_Notes_Shipping2",
                    shippingMethods[0].Name,
                    (8.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)),
                    shippingMethods[1].Name,
                    (13.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)),
                    (2.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)),
                    sb.ToString());
            }
            if (shippingMethods.Count > 0)
            {
                sb = new StringBuilder();
                sb.Append(shippingMethods[0].ID);
                sb.Append(":");
                sb.Append((7.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)));
                sb.Append(":");
                sb.Append((1.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)));
                shippingNotes += controller.GlobalResourceString("CSVImport_Notes_Shipping3",
                    shippingMethods[0].Name,
                    (7.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)),
                    (1.95).ToString("G", CultureInfo.GetCultureInfo(cultureCode)),
                    sb.ToString());
            }
            columnSpec.Add(new ShippingColumnSpec(colIndex++, "ShippingOptions", cultureCode, shippingNotes, shippingExample));

            if (!SiteClient.EnableEvents || mixedModeEnabled)
            {
                string durationNotes = controller.GlobalResourceString("CSVImport_Notes_Duration");
                if (mixedModeEnabled)
                {
                    durationNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_Duration_MixedMode"));
                }
                columnSpec.Add(new SimpleColumnSpec(colIndex++, "Duration", CustomFieldType.Int, durationNotes, false, cultureCode, "3"));
                string startDateNotes = controller.GlobalResourceString("CSVImport_Notes_StartDTTM");
                if (mixedModeEnabled)
                {
                    startDateNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_StartDTTM_MixedMode"));
                }
                columnSpec.Add(new DateTimeColumnSpec(colIndex++, "StartDTTM", startDateNotes, false, cultureCode, "Start"));
                string endDateNotes = controller.GlobalResourceString("CSVImport_Notes_EndDTTM");
                if (mixedModeEnabled)
                {
                    endDateNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_EndDTTM_MixedMode"));
                }
                columnSpec.Add(new DateTimeColumnSpec(colIndex++, "EndDTTM", endDateNotes, false, cultureCode, "End"));
                string autoRelistNotes = controller.GlobalResourceString("CSVImport_Notes_AutoRelist");
                if (mixedModeEnabled)
                {
                    autoRelistNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_AutoRelist_MixedMode"));
                }
                columnSpec.Add(new SimpleColumnSpec(colIndex++, "AutoRelist", CustomFieldType.Int, autoRelistNotes, false, cultureCode, "3"));
                string goodTilCanceledNotes = controller.GlobalResourceString("CSVImport_Notes_GoodTilCanceled");
                if (mixedModeEnabled)
                {
                    goodTilCanceledNotes += (" " + controller.GlobalResourceString("CSVImport_Notes_GoodTilCanceled_MixedMode"));
                }
                columnSpec.Add(new SimpleColumnSpec(colIndex++, "GoodTilCanceled", CustomFieldType.Boolean, goodTilCanceledNotes, false, cultureCode, "False"));
            }
            
            foreach (CustomField field in CommonClient.GetCustomFields("Item", 0, 0, "Name", true).List)
            {
                string example = string.Empty;
                string notes = string.Empty;
                string fieldName = controller.CustomFieldResourceOrDefaultString(field.Name);
                fieldName = string.IsNullOrEmpty(fieldName) ? field.Name : fieldName;
                if (field.Name != fieldName)
                {
                    notes += fieldName;
                }
                string helpText = controller.CustomFieldResourceOrDefaultString(field.Name + "_Help");
                if (!string.IsNullOrEmpty(helpText))
                {
                    notes += ("<p>" + controller.CustomFieldResourceOrDefaultString(field.Name) + "</p>");
                }
                switch (field.Type)
                {
                    case CustomFieldType.Boolean:
                        //    sb.AppendLine("<p>\"true\" or \"false\"</p>");
                        example = "True";
                        break;
                    case CustomFieldType.Int:
                        //    sb.AppendLine("<p>An integer value like 1, 2, 3, etc...</p>");
                        example = "3";
                        break;
                    case CustomFieldType.String:
                        //    sb.AppendLine("<p>A text value</p>");
                        example = "Example";
                        break;
                    case CustomFieldType.Enum:
                        sb = new StringBuilder();
                        sb.AppendLine("<ul>");
                        foreach (ListItem item in field.Enumeration)
                        {
                            if (!string.IsNullOrEmpty(item.Value))
                            {
                                sb.AppendLine("<li>");
                                sb.AppendLine(item.Value);
                                string localizedValueName = controller.CustomFieldResourceOrDefaultString(item.Name);
                                if (!string.IsNullOrEmpty(localizedValueName) && item.Value != localizedValueName)
                                {
                                    sb.Append(" " + controller.GlobalResourceString("CSVImport_Notes_CustomFieldChoice", localizedValueName));
                                }
                                sb.AppendLine("</li>");
                                example = item.Value;
                            }
                        }
                        sb.AppendLine("</ul>");
                        if (field.Required)
                        {
                            notes += controller.GlobalResourceString("CSVImport_Notes_CustomFieldRequired", sb.ToString());
                        }
                        else
                        {
                            notes += controller.GlobalResourceString("CSVImport_Notes_CustomFieldOptional", sb.ToString());
                        }
                        break;
                    case CustomFieldType.DateTime:
                        //    sb.AppendLine("<p>A date & time value</p>");
                        example = DateTime.UtcNow.ToString(CultureInfo.GetCultureInfo(cultureCode));
                        break;
                    case CustomFieldType.Decimal:
                        //    sb.AppendLine("<p>A decimal value</p>");
                        example = 3.0M.ToString(CultureInfo.GetCultureInfo(cultureCode));
                        break;
                }
                columnSpec.Add(CustomFieldColumnSpec.Create(colIndex++, field, cultureCode, notes, example));
            }            

            return columnSpec;
        }

        /// <summary>
        /// returns the header row of a CSV file which is valid for the currently admin setting configuration
        /// </summary>
        /// <param name="columns">the column specs to be included</param>
        public static string GetCSVTemplate(List<IColumnSpec> columns)
        {
            StringBuilder sb = new StringBuilder();

            foreach (IColumnSpec spec in columns)
            {
                string value = spec.Name;

                if (value.Contains(",") || value.Contains("\""))
                {
                    //value must be quoted
                    value = value.Replace("\"", "\"\"");
                    value = "\"" + value + "\"";
                }

                sb.Append(value);
                sb.Append(",");
            }
            sb.Remove(sb.Length - 1, 1);

            return sb.ToString();
        }

        /// <summary>
        /// checks the basic validity of proposed import data based on the specified list of columns
        /// </summary>
        /// <param name="columns">the column specs to validate for</param>
        /// <param name="importData">the data to validate</param>
        public static bool PreValidate(List<IColumnSpec> columns, ImportData importData)
        {
            bool retVal = true;

            foreach (ImportListing listingRow in importData.ListingData)
            {
                foreach (IColumnSpec columnSpec in columns)
                {
                    string lastColName = string.Empty;
                    try
                    {
                        lastColName = columnSpec.Name;
                        bool thisCheck = columnSpec.Validate(listingRow);
                        if (!thisCheck) listingRow.Status = ImportListingStatus.Validation;
                        retVal = thisCheck && retVal;
                    }
                    catch (Exception e)
                    {
                        string errMessage = string.Format("Error evaluating [{0}].  See previous issues.", lastColName, FlattenExceptionMessages(e));
                        listingRow.Disposition.Add(errMessage);
                        listingRow.Status = ImportListingStatus.Exception;
                    }
                }
            }

            if (!retVal)
            {
                importData.Status = ImportListingStatus.Validation;
                importData.Disposition = "CSV Pre-Validation Error";
            }

            return retVal;
        }

        /// <summary>
        /// tranforms a row of CSV data into UserInput key/value pairs to be submitted to the BLL
        /// </summary>
        /// <param name="columns">list of columns specs</param>
        /// <param name="importData">list of data to be parsed</param>
        /// <param name="input">the container to stored the parsed data</param>
        /// <param name="commitIntent">true to treat as "publish ready" or false to parse for creation of drafts</param>
        public static void Translate(List<IColumnSpec> columns, ImportListing importData, Dictionary<string, string> input, bool commitIntent)
        {
            try
            {
                foreach (IColumnSpec columnSpec in columns)
                {
                    columnSpec.Translate(input, importData, commitIntent);
                }
            } catch (Exception e)
            {
                importData.Disposition.Add(FlattenExceptionMessages(e));
                importData.Status = ImportListingStatus.Exception;
            }
        }

        /// <summary>
        /// converts an exception object to a readable string including applicable inner exception messages
        /// </summary>
        /// <param name="e">an exception</param>
        public static string FlattenExceptionMessages(Exception e)
        {
            string retVal = e.Message;
            while (e.InnerException != null)
            {
                e = e.InnerException;
                retVal += ": " + e.Message;
            }
            return retVal;
        }

        /// <summary>
        /// parses the specified CSV file into a collection of rows to be processed
        /// </summary>
        /// <param name="fileName">the full file path to be read</param>
        /// <param name="headingRow">indicates whether a header row is expected (default: true)</param>
        public static ImportData Parse(string fileName, bool headingRow = true)
        {            
            StreamReader sr = new StreamReader(fileName);
            ImportData retVal = Parse(sr.BaseStream, headingRow);
            sr.Close();
            return retVal;
        }

        /// <summary>
        /// parses the specified CSV input stream into a collection of rows to be processed
        /// </summary>
        /// <param name="inputStream">the input stream to be read</param>
        /// <param name="headingRow">indicates whether a header row is expected (default: true)</param>
        public static ImportData Parse(Stream inputStream, bool headingRow = true)
        {            
            ImportData retVal = new ImportData();               

            using (TextFieldParser parser = new TextFieldParser(inputStream))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");

                string[] headerRow = null;
                try
                {
                    headerRow = parser.ReadFields();
                }
                catch (MalformedLineException)
                {
                    //malformed CSV line                                            
                    retVal.Status = ImportListingStatus.ParseError;
                    retVal.Disposition = "Header row malformed";
                }
                catch (Exception e)
                {
                    //other exception
                    retVal.Status = ImportListingStatus.Exception;
                    retVal.Disposition = "Exception: " + e.Message;
                }

                int count = 1;
                string[] currentRow = null;
                while (!parser.EndOfData)
                {
                    ImportListing newListing = new ImportListing();
                    try
                    {
                        retVal.ListingData.Add(newListing);
                        newListing.Line = count++;
                        currentRow = parser.ReadFields();
                        for (int i = 0; i < headerRow.Count(); i++)
                        {
                            if (currentRow[i].Equals("true", StringComparison.OrdinalIgnoreCase))
                            {
                                currentRow[i] = "True";
                            }
                            else if (currentRow[i].Equals("false", StringComparison.OrdinalIgnoreCase))
                            {
                                currentRow[i] = "False";
                            }
                            newListing.ColumnData.Add(headerRow[i], currentRow[i]);
                        }
                    }
                    catch (MalformedLineException)
                    {
                        //malformed CSV line                                                
                        newListing.Status = ImportListingStatus.ParseError;
                        newListing.Disposition.Add("Malformed Line: \"" + parser.ErrorLine + "\"");
                        retVal.Status = ImportListingStatus.ParseError;
                        retVal.Disposition = "Data row(s) malformed";
                    }
                    catch (Exception e)
                    {
                        //other exception
                        newListing.Status = ImportListingStatus.Exception;
                        newListing.Disposition.Add("Exception: " + e.Message);
                        retVal.Status = ImportListingStatus.Exception;
                        retVal.Disposition = "Data row exception(s)";
                    }
                }
            }
            return retVal;
        }

    }
}
