using System.Collections.Generic;
using LettrLabs.UrlShorterner.Core.Domain;

namespace LettrLabs.UrlShorterner.Core.Messages
{
    public class ListResponse
    {
        public List<ShortUrlEntity> UrlList { get; set; }

        public ListResponse() { }
        public ListResponse(List<ShortUrlEntity> list)
        {
            UrlList = list;
        }
    }
}