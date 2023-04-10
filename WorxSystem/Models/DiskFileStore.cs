using System;
using System.Configuration;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Net;
using System.Web;
using System.IO;
using System.Web.Hosting;
using RainWorx.FrameWorx.Imaging;
using System.Drawing;

namespace RainWorx.FrameWorx.MVC.Models
{
    internal static class DiskFileStore
    {
        //private static readonly string _uploadsFolder = ResolvePath(Strings.MVC.ListingImagePath); 
        //public static readonly string _uploadsFolder = ResolvePath(ConfigurationManager.AppSettings["ImageSavePath"]); 
       
        //private static string ResolvePath(string path)
        //{
        //    if (path.StartsWith("~"))
        //    {
        //        return HostingEnvironment.MapPath(path);
        //    } else
        //    {
        //        return path;
        //    }
        //}

        public static string ReadTempImage(string url)
        {
            const int BYTESTOREAD = 10000;
            WebRequest myRequest = WebRequest.Create(url);
            WebResponse myResponse = myRequest.GetResponse();
            Stream ReceiveStream = myResponse.GetResponseStream();
            BinaryReader br = new BinaryReader(ReceiveStream);
            MemoryStream memstream = new MemoryStream();
            byte[] bytebuffer = new byte[BYTESTOREAD];
            int BytesRead = br.Read(bytebuffer, 0, BYTESTOREAD);
            while (BytesRead > 0)
            {
                memstream.Write(bytebuffer, 0, BytesRead);
                BytesRead = br.Read(bytebuffer, 0, BYTESTOREAD);
            }
            Bitmap Img = new Bitmap(memstream);
            return Img.PixelFormat.ToString();
        } 


        //public static void WriteTempImage(string fileName, string text)
        //{            
        //    int textLength = text.Length;
        //    int fontSize = 16;
        //    int width = (fontSize * textLength) - ((textLength * fontSize) / 3);
        //    int height = fontSize + 20;

        //    // Initialize graphics
        //    RectangleF rectF = new RectangleF(0, 0, width, height);
        //    Bitmap pic = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        //    Graphics g = Graphics.FromImage(pic);
        //    g.SmoothingMode = SmoothingMode.AntiAlias;
        //    g.TextRenderingHint = TextRenderingHint.AntiAlias;

        //    // Set colors            
        //    Color fontColor = Color.FromName("Black");
        //    Color rectColor = Color.FromName("White");
        //    SolidBrush fgBrush = new SolidBrush(fontColor);
        //    SolidBrush bgBrush = new SolidBrush(rectColor);

        //    g.FillRectangle(bgBrush, rectF);

        //    // Load font   
        //    FontFamily fontFamily = new FontFamily(GenericFontFamilies.Monospace);
        //    FontStyle style = FontStyle.Regular;
        //    Font font = new Font(fontFamily, fontSize, style, GraphicsUnit.Pixel);

        //    StringFormat format = new StringFormat();
        //    format.Alignment = StringAlignment.Center;
        //    format.LineAlignment = StringAlignment.Center;

        //    // Finally, draw the font
        //    g.DrawString(text, font, fgBrush, rectF, format);
        //    string saveTempFile = Path.Combine(_uploadsFolder, fileName);
            
        //    pic.Save(saveTempFile, ImageFormat.Png);                        
        //}

        //public static void WriteTempLogo(string fileName, string text)
        //{
        //    int textLength = text.Length;
        //    int fontSize = 16;
        //    int width = (fontSize * textLength) - ((textLength * fontSize) / 3);
        //    int height = fontSize + 20;

        //    // Initialize graphics
        //    RectangleF rectF = new RectangleF(0, 0, width, height);
        //    Bitmap pic = new Bitmap(width, height, PixelFormat.Format24bppRgb);
        //    Graphics g = Graphics.FromImage(pic);
        //    g.SmoothingMode = SmoothingMode.AntiAlias;
        //    g.TextRenderingHint = TextRenderingHint.AntiAlias;

        //    // Set colors            
        //    Color fontColor = Color.FromName("Black");
        //    Color rectColor = Color.FromName("White");
        //    SolidBrush fgBrush = new SolidBrush(fontColor);
        //    SolidBrush bgBrush = new SolidBrush(rectColor);

        //    g.FillRectangle(bgBrush, rectF);

        //    // Load font   
        //    FontFamily fontFamily = new FontFamily(GenericFontFamilies.Monospace);
        //    FontStyle style = FontStyle.Regular;
        //    Font font = new Font(fontFamily, fontSize, style, GraphicsUnit.Pixel);

        //    StringFormat format = new StringFormat();
        //    format.Alignment = StringAlignment.Center;
        //    format.LineAlignment = StringAlignment.Center;

        //    // Finally, draw the font
        //    g.DrawString(text, font, fgBrush, rectF, format);
        //    string saveTempFile = Path.Combine(_logoFolder, fileName);

        //    pic.Save(saveTempFile, ImageFormat.Png);
        //}

        public static Stream CreateTempImageAsStream(string text)
        {
            int textLength = text.Length;
            int fontSize = 18;
            int width = (fontSize * textLength) - ((textLength * fontSize) / 3);
            int height = fontSize + 20;

            // Initialize graphics
            RectangleF rectF = new RectangleF(0, 0, width, height);
            Bitmap pic = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            Graphics g = Graphics.FromImage(pic);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;            

            // Set colors            
            Color fontColor = Color.FromName("Black");
            Color rectColor = Color.FromName("White");
            SolidBrush fgBrush = new SolidBrush(fontColor);
            SolidBrush bgBrush = new SolidBrush(rectColor);

            g.FillRectangle(bgBrush, rectF);

            // Load font   
            FontFamily fontFamily = new FontFamily(GenericFontFamilies.Monospace);
            FontStyle style = FontStyle.Regular;
            Font font = new Font(fontFamily, fontSize, style, GraphicsUnit.Pixel);

            StringFormat format = new StringFormat();
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;

            // Finally, draw the font
            g.DrawString(text, font, fgBrush, rectF, format);

            MemoryStream ms = new MemoryStream();
            pic.Save(ms, ImageFormat.Png);

            return ms;
        }

        //public static void TryWrite()
        //{
        //    string saveTempFile = Path.Combine(_uploadsFolder, Guid.NewGuid().ToString("D"));

        //    try
        //    {
        //        FileStream fs = new FileStream(saveTempFile, FileMode.CreateNew, FileAccess.Write);
        //        fs.Close();                
        //    } catch
        //    {
        //        throw new Exception("Couldn't write to image folder");
        //    }            

        //    try
        //    {
        //        File.Delete(saveTempFile);
        //    } catch
        //    {
        //        throw new Exception("Couldn't delete temporary file");
        //    }
        //}        

        //public static string SaveUploadedFile(HttpPostedFileBase fileBase, int thumbX, int thumbY, int imageX, int imageY)
        //{
        //    var identifier = Guid.NewGuid();
        //    string folder = DateTime.Now.ToString(Strings.Formats.SortableDatePattern);
        //    string newPath = Path.Combine(_uploadsFolder, folder);
        //    Directory.CreateDirectory(newPath);
        //    string saveLocation = Path.Combine(newPath, identifier.ToString());            
        //    fileBase.SaveAs(saveLocation);

        //    Image current = Transformer.LoadImage(saveLocation);
        //    Transformer.SaveImage(Transformer.CenterCrop(current, thumbX, thumbY), saveLocation + Strings.MVC.CenterCropImageSuffix);
        //    Transformer.SaveImage(Transformer.FitWithin(current, thumbX, thumbY), saveLocation + Strings.MVC.CenterFitImageSuffix);
        //    Transformer.SaveImage(Transformer.FitWithin(current, imageX, imageY), saveLocation + Strings.MVC.StandardSizeImageSuffix);
        //    current.Dispose();

        //    return folder + identifier;
        //}

        //public static readonly string _logoFolder = ResolvePath(Strings.MVC.LogoImagePath);        

        //public static string SaveLogo(HttpPostedFileBase fileBase, string fileName)
        //{
        //    if (!Directory.Exists(_logoFolder)) Directory.CreateDirectory(_logoFolder);

        //    string saveLocation = Path.Combine(_logoFolder, fileName);
        //    fileBase.SaveAs(saveLocation);

        //    //Image current = Transformer.LoadImage(saveLocation);            
        //    //Transformer.SaveImage(Transformer.FitWithin(current, current.Width, current.Height), saveLocation + ".png");

        //    return saveLocation;
        //}     
    }
}