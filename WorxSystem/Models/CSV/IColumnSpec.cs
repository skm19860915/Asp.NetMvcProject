using System.Collections.Generic;

namespace RainWorx.FrameWorx.MVC.Models.CSV
{
    public interface IColumnSpec
    {
        ColumnHelpSpec ToHelp();
        bool Validate(ImportListing csvRow);
        void Translate(Dictionary<string, string> input, ImportListing csvRow, bool commitIntent);

        string Name { get; }
    }
}