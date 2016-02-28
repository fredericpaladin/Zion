using System.ComponentModel;

namespace SimpleFS
{
    internal enum LookupAlgorithm
    {
        [Description("None")] // This is the display text in the drop down
        None,

        [Description("Circular")]
        Circular
    }
}