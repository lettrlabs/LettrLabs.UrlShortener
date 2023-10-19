namespace LettrLabs.UrlShorterner.Core.Messages
{
    public class UrlClickStatsByOrderIdsRequest
    {
        public UrlClickStatsByOrderIdsRequest()
        {
            OrderIds = new List<int>();
        }
        public IList<int> OrderIds { get; }

        public UrlClickStatsByOrderIdsRequest(IList<int> orderIds)
        {
            OrderIds = orderIds;
        }
    }
}