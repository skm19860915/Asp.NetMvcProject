using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RainWorx.FrameWorx.MVC.Models
{
    public enum ResultDisposition
    {
        Pass,
        Fail,
        Warning,
        Inconclusive
    }

    public class CheckItem
    {
        public string Title { private set; get; }
        public string CheckText { private set; get; }
        public string ResultText { private set; get; }
        public ResultDisposition Disposition { private set; get; }

        public CheckItem(string title, string checkText, ResultDisposition disposition, string resultText)
        {
            Title = title;
            CheckText = checkText;
            ResultText = resultText;
            Disposition = disposition;
        }

        public CheckItem(string title, string checkText, ResultDisposition disposition)
        {
            Title = title;
            CheckText = checkText;
            ResultText = string.Empty;
            Disposition = disposition;
        }
    }

    public class SiteCheck
    {
        public string Title;
        public List<CheckItem> CheckItems;
        public ResultDisposition Disposition;

        public void AddCheckItem(CheckItem item)
        {
            CheckItems.Add(item);
            if (item.Disposition == ResultDisposition.Fail && (Disposition == ResultDisposition.Pass || Disposition == ResultDisposition.Warning))
            {
                Disposition = ResultDisposition.Fail;
                return;
            }
            if (item.Disposition == ResultDisposition.Warning && Disposition == ResultDisposition.Pass)
            {
                Disposition = ResultDisposition.Warning;
                return;
            }
        }

        public SiteCheck(string title)
        {
            Title = title;
            CheckItems = new List<CheckItem>();
            Disposition = ResultDisposition.Pass;
        }
    }
}