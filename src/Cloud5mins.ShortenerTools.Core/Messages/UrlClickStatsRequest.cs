namespace LettrLabs.UrlShorterner.Core.Messages
{
    public class UrlClickStatsRequest
    {
        public string Vanity { get; set; }

        public UrlClickStatsRequest(string vanity)
        {
            Vanity = vanity;
        }
    }
}