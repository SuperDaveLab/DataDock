using System;

namespace SchemaViz.Gui.ViewModels.Diagram
{
    public class SchemaExportRequestedEventArgs : EventArgs
    {
        public SchemaExportRequestedEventArgs(string jsonContent, string suggestedFileName)
        {
            JsonContent = jsonContent;
            SuggestedFileName = suggestedFileName;
        }

        public string JsonContent { get; }

        public string SuggestedFileName { get; }
    }
}
