namespace LettrLabs.UrlShorterner.Core.Messages
{
    public class UrlClickStatsByOrderIdsRequest
    {
        public IList<int> OrderIds { get; }

        public UrlClickStatsByOrderIdsRequest(IList<int> orderIds)
        {
            OrderIds = orderIds;
        }
    }
}