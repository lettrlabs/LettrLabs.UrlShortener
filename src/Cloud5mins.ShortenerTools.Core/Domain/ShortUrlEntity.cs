using LettrLabs.UrlShorterner.Core.Messages;
using Microsoft.Azure.Cosmos.Table;
using System.Text.Json;

namespace LettrLabs.UrlShorterner.Core.Domain
{
    public class ShortUrlEntity : TableEntity
    {
        private string _title;
        private ShortRequest _input;

        private string _activeUrl { get; set; }

        public string ActiveUrl
        {
            get
            {
                if (string.IsNullOrEmpty(_activeUrl))
                    _activeUrl = GetActiveUrl();
                return _activeUrl;
            }
        }
        public string Url { get; set; }
        public string Title { get; set; }
        public string ShortUrl { get; set; }
        public int ProfileId { get; set; }
        public int OrderId { get; set; }
        public int OrderRecipientId { get; set; }
        public int ProfileRecipientId { get; set; }
        public string OrderRecipientName { get; set; }
        public int Clicks { get; set; }
        public bool? IsArchived { get; set; }
        public string SchedulesPropertyRaw { get; set; }
        private List<Schedule> _schedules { get; set; }

        [IgnoreProperty]
        public List<Schedule> Schedules
        {
            get
            {
                if (_schedules == null)
                {
                    _schedules = string.IsNullOrEmpty(SchedulesPropertyRaw)
                        ? new List<Schedule>()
                        : JsonSerializer.Deserialize<Schedule[]>(SchedulesPropertyRaw).ToList();
                }
                return _schedules;
            }
            set => _schedules = value;
        }

        public ShortUrlEntity() { }

        public ShortUrlEntity(string longUrl, string endUrl)
        {
            Initialize(longUrl, endUrl, string.Empty, null);
        }

        public ShortUrlEntity(string longUrl, string endUrl, string title, Schedule[] schedules)
        {
            Initialize(longUrl, endUrl, title, schedules);
        }

        public ShortUrlEntity(string longUrl, string endUrl, string title, ShortRequest input)
        {
            Initialize(longUrl, endUrl, title, input.Schedules);
            OrderId = input.OrderId;
            OrderRecipientId = input.OrderRecipientId;
            ProfileId = input.ProfileId;
            ProfileRecipientId = input.ProfileRecipientId;
            OrderRecipientName = input.OrderRecipientName;
        }

        public void SetKeys()
        {
            RowKey = ShortUrl.Split('/').Last();
            PartitionKey = RowKey.First().ToString();
        }

        private void Initialize(string longUrl, string endUrl, string title, Schedule[] schedules)
        {
            PartitionKey = endUrl.First().ToString();
            RowKey = endUrl;
            Url = longUrl;
            Title = title;
            Clicks = 0;
            IsArchived = false;

            if (schedules?.Length > 0)
            {
                Schedules = schedules.ToList();
                SchedulesPropertyRaw = JsonSerializer.Serialize(Schedules);
            }
        }

        private string GetActiveUrl()
        {
            if (Schedules != null)
                return GetActiveUrl(DateTime.UtcNow);
            return Url;
        }
        private string GetActiveUrl(DateTime pointInTime)
        {
            var link = Url;
            var active = Schedules.Where(s =>
                s.End > pointInTime && //hasn't ended
                s.Start < pointInTime //already started
                ).OrderBy(s => s.Start); //order by start to process first link

            foreach (var sched in active.ToArray())
            {
                if (sched.IsActive(pointInTime))
                {
                    link = sched.AlternativeUrl;
                    break;
                }
            }
            return link;
        }
    }

}