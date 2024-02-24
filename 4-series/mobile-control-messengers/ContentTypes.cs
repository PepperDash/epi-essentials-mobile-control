namespace PepperDash.Essentials.AppServer
{
    public class SourceSelectMessageContent
    {
        public string SourceListItem { get; set; }
        public string SourceListKey { get; set; }
    }

    public class DirectRoute
    {
        public string SourceKey { get; set; }
        public string DestinationKey { get; set; }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="b"></param>
    public delegate void PressAndHoldAction(bool b);
}
