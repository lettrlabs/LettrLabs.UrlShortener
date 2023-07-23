using LettrLabs.UrlShorterner.Core.Messages;
using Microsoft.Azure.Cosmos.Table;
using System.Text.Json;

namespace LettrLabs.UrlShorterner.Core.Domain
{
    public class ShortUrlEntity : TableEntity
    {
        private string _title;
        private ShortRequest _input;

        public string Url { get; set; }
        private string _activeUrl { get; set; }

        public string ActiveUrl
        {
            get
            {
                if (String.IsNullOrEmpty(_activeUrl))
                    _activeUrl = GetActiveUrl();
                return _activeUrl;
            }
        }


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
                    if (string.IsNullOrEmpty(SchedulesPropertyRaw))
                    {
                        _schedules = new List<Schedule>();
                    }
                    else
                    {
                        _schedules = JsonSerializer.Deserialize<Schedule[]>(SchedulesPropertyRaw).ToList<Schedule>();
                    }
                }
                return _schedules;
            }
            set
            {
                _schedules = value;
            }
        }

        public ShortUrlEntity() { }

        //public ShortUrlEntity(ShortRequest shortRequest)
        //{
        //    Initialize(shortRequest);
        //}

        public ShortUrlEntity(string longUrl, string endUrl)
        {
            Initialize(longUrl, endUrl, string.Empty, null);
        }

        //public ShortUrlEntity(string longUrl, string endUrl, Schedule[] schedules)
        //{
        //    Initialize(longUrl, endUrl, string.Empty, schedules);
        //}

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

        private void Initialize(string longUrl, string endUrl, string title, Schedule[] schedules)
        {
            PartitionKey = endUrl.First().ToString();
            RowKey = endUrl;
            Url = longUrl;
            Title = title;
            Clicks = 0;
            IsArchived = false;

            if(schedules?.Length>0)
            {
                Schedules = schedules.ToList<Schedule>();
                SchedulesPropertyRaw = JsonSerializer.Serialize<List<Schedule>>(Schedules);
            }
        }

        //public static ShortUrlEntity GetEntity(string longUrl, string endUrl, string title, Schedule[] schedules)
        //{
        //    return new ShortUrlEntity
        //    {
        //        PartitionKey = endUrl.First().ToString(),
        //        RowKey = endUrl,
        //        Url = longUrl,
        //        Title = title,
        //        Schedules = schedules.ToList<Schedule>()
        //    };
        //}

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