using LettrLabs.UrlShorterner.Core.Domain;

namespace LettrLabs.UrlShorterner.Core.Messages
{
    public class ShortRequest
    {
        //public string Vanity { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public int ProfileId { get; set; }
        public int OrderId { get; set; }
        public int OrderRecipientId { get; set; }
        public int ProfileRecipientId { get; set; }
        public string OrderRecipientName { get; set; }
        public Schedule[] Schedules { get; set; }
    }
}