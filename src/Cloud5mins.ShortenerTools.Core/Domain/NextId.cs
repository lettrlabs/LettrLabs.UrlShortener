using Microsoft.Azure.Cosmos.Table;

namespace LettrLabs.UrlShorterner.Core.Domain
{
    public class NextId : TableEntity
    {
        public int Id { get; set; }
    }
}