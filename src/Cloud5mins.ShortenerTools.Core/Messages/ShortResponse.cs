namespace LettrLabs.UrlShorterner.Core.Messages
{
    public class ShortResponse
    {
        public string ShortUrl { get; set; }
        public string LongUrl { get; set; }
        public string Title { get; set; }
        public int OrderRecipientId { get; set; }

        public ShortResponse() { }
        public ShortResponse(string scheme, string host, string longUrl, string endUrl, string title, int orderRecipientId)
        {
            LongUrl = longUrl;
            ShortUrl = string.Concat(scheme, "://", host, "/", endUrl);
            Title = title;
            OrderRecipientId = orderRecipientId;
        }
    }
}