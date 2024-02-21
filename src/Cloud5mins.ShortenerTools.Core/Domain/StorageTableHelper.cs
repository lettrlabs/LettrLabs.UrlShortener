using Microsoft.Azure.Cosmos.Table;
using System.Text.Json;

namespace LettrLabs.UrlShorterner.Core.Domain
{
    public class StorageTableHelper
    {
        private string StorageConnectionString { get; set; }

        public StorageTableHelper(string storageConnectionString)
        {
            StorageConnectionString = storageConnectionString;
        }
        public CloudStorageAccount CreateStorageAccountFromConnectionString()
        {
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(StorageConnectionString);
            return storageAccount;
        }

        private async Task<CloudTable> GetTableAsync(string tableName)
        {
            CloudStorageAccount storageAccount = CreateStorageAccountFromConnectionString();
            CloudTableClient tableClient = storageAccount.CreateCloudTableClient(new TableClientConfiguration());
            CloudTable table = tableClient.GetTableReference(tableName);
            await table.CreateIfNotExistsAsync();

            return table;
        }

        private async Task<CloudTable> GetUrlsTableAsync()
        {
            CloudTable table = await GetTableAsync("UrlsDetails");
            return table;
        }

        private async Task<CloudTable> GetStatsTableAsync()
        {
            CloudTable table = await GetTableAsync("ClickStats");
            return table;
        }

        public async Task<ShortUrlEntity> GetShortUrlEntityAsync(ShortUrlEntity row)
        {
            TableOperation selOperation = TableOperation.Retrieve<ShortUrlEntity>(row.PartitionKey, row.RowKey);
            TableResult result = await (await GetUrlsTableAsync()).ExecuteAsync(selOperation);
            ShortUrlEntity eShortUrl = result.Result as ShortUrlEntity;
            return eShortUrl;
        }

        public async Task<List<ShortUrlEntity>> GetAllShortUrlEntitiesAsync()
        {
            var tblUrls = await GetUrlsTableAsync();
            TableContinuationToken token = null;
            var lstShortUrl = new List<ShortUrlEntity>();
            do
            {
                // Retrieving all entities that are NOT the NextId entity 
                // (it's the only one in the partition "KEY")
                TableQuery<ShortUrlEntity> rangeQuery = new TableQuery<ShortUrlEntity>().Where(
                    filter: TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.NotEqual, "KEY"));

                var queryResult = await tblUrls.ExecuteQuerySegmentedAsync(rangeQuery, token);
                lstShortUrl.AddRange(queryResult.Results);
                token = queryResult.ContinuationToken;
            } while (token != null);
            return lstShortUrl;
        }

        public async Task<List<ShortUrlEntity>> GetAllShortUrlEntitiesByOrderIdsAsync(IList<int> orderIds)
        {
            var tblUrls = await GetUrlsTableAsync();
            TableContinuationToken token = null;
            var lstShortUrl = new List<ShortUrlEntity>();
            do
            {
                string filter = string.Join(" or ", orderIds.Select(id => TableQuery.GenerateFilterConditionForInt("OrderId", QueryComparisons.Equal, id)));
                //Append filter where "clicks" > 0
                filter = TableQuery.CombineFilters(filter, TableOperators.And, TableQuery.GenerateFilterConditionForInt("Clicks", QueryComparisons.GreaterThan, 0));
                // Retrieving all entities that are NOT the NextId entity 
                // (it's the only one in the partition "KEY")
                TableQuery<ShortUrlEntity> rangeQuery = new TableQuery<ShortUrlEntity>().Where(filter);

                var queryResult = await tblUrls.ExecuteQuerySegmentedAsync(rangeQuery, token);
                lstShortUrl.AddRange(queryResult.Results);
                token = queryResult.ContinuationToken;
            } while (token != null);
            return lstShortUrl;
        }

        /// <summary>
        /// Returns the ShortUrlEntity of the <paramref name="vanity"/>
        /// </summary>
        /// <param name="vanity"></param>
        /// <returns>ShortUrlEntity</returns>
        public async Task<ShortUrlEntity> GetShortUrlEntityByVanityAsync(string vanity)
        {
            var tblUrls = await GetUrlsTableAsync();
            TableContinuationToken token = null;
            ShortUrlEntity shortUrlEntity;
            do
            {
                TableQuery<ShortUrlEntity> query = new TableQuery<ShortUrlEntity>().Where(
                    filter: TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, vanity));
                var queryResult = await tblUrls.ExecuteQuerySegmentedAsync(query, token);
                shortUrlEntity = queryResult.Results.FirstOrDefault()!;
            } while (token != null);

            return shortUrlEntity;
        }
        public async Task SaveClickStatsEntityAsync(ClickStatsEntity newStats)
        {
            TableOperation insOperation = TableOperation.InsertOrMerge(newStats);
            TableResult result = await (await GetStatsTableAsync()).ExecuteAsync(insOperation);
        }

        public async Task<ShortUrlEntity> SaveShortUrlEntityAsync(ShortUrlEntity newShortUrl)
        {
            // serializing the collection easier on json shares
            //newShortUrl.SchedulesPropertyRaw = JsonSerializer.Serialize<List<Schedule>>(newShortUrl.Schedules);

            TableOperation insOperation = TableOperation.InsertOrMerge(newShortUrl);
            TableResult result = await (await GetUrlsTableAsync()).ExecuteAsync(insOperation);
            ShortUrlEntity eShortUrl = result.Result as ShortUrlEntity;
            return eShortUrl;
        }

        public async Task<bool> IfShortUrlEntityExistByVanityAsync(string vanity)
        {
            ShortUrlEntity shortUrlEntity = await GetShortUrlEntityByVanityAsync(vanity);
            return (shortUrlEntity != null);
        }

        public async Task<bool> IfShortUrlEntityExistAsync(ShortUrlEntity row)
        {
            ShortUrlEntity eShortUrl = await GetShortUrlEntityAsync(row);
            return (eShortUrl != null);
        }
        public async Task<int> GetNextTableIdAsync()
        {
            //Get current ID
            TableOperation selOperation = TableOperation.Retrieve<NextId>("1", "KEY");
            TableResult result = await (await GetUrlsTableAsync()).ExecuteAsync(selOperation);
            NextId entity = result.Result as NextId;

            if (entity == null)
            {
                entity = new NextId
                {
                    PartitionKey = "1",
                    RowKey = "KEY",
                    Id = 1024
                };
            }
            entity.Id++;

            //Update
            TableOperation updOperation = TableOperation.InsertOrMerge(entity);

            // Execute the operation.
            await (await GetUrlsTableAsync()).ExecuteAsync(updOperation);

            return entity.Id;
        }

        public async Task<ShortUrlEntity> UpdateShortUrlEntityAsync(ShortUrlEntity urlEntity)
        {
            ShortUrlEntity originalUrl = await GetShortUrlEntityAsync(urlEntity);
            originalUrl.Url = urlEntity.Url;
            originalUrl.Title = urlEntity.Title;
            originalUrl.SchedulesPropertyRaw = JsonSerializer.Serialize(urlEntity.Schedules);

            return await SaveShortUrlEntityAsync(originalUrl);
        }

        public async Task<ShortUrlEntity> UpdateShortUrlEntityUrlAsync(ShortUrlEntity urlEntity)
        {
            ShortUrlEntity originalUrl = await GetShortUrlEntityAsync(urlEntity);
            originalUrl.Url = urlEntity.Url;

            return await SaveShortUrlEntityAsync(originalUrl);
        }

        public async Task<List<ClickStatsEntity>> GetAllStatsByVanityAsync(string vanity)
        {
            var tblUrls = await GetStatsTableAsync();
            TableContinuationToken token = null;
            var lstShortUrl = new List<ClickStatsEntity>();
            do
            {
                TableQuery<ClickStatsEntity> rangeQuery;

                if (string.IsNullOrEmpty(vanity))
                {
                    rangeQuery = new TableQuery<ClickStatsEntity>();
                }
                else
                {
                    rangeQuery = new TableQuery<ClickStatsEntity>().Where(
                    filter: TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, vanity));
                }

                var queryResult = await tblUrls.ExecuteQuerySegmentedAsync(rangeQuery, token);
                lstShortUrl.AddRange(queryResult.Results as List<ClickStatsEntity>);
                token = queryResult.ContinuationToken;
            } while (token != null);
            return lstShortUrl;
        }

        public async Task<ShortUrlEntity> ArchiveShortUrlEntityAsync(ShortUrlEntity urlEntity)
        {
            ShortUrlEntity originalUrl = await GetShortUrlEntityAsync(urlEntity);
            originalUrl.IsArchived = true;

            return await SaveShortUrlEntityAsync(originalUrl);
        }
    }
}