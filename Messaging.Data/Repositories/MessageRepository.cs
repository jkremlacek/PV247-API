﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Messaging.Contract.Models;
using Messaging.Contract.Repositories;
using Messaging.Data.Models;
using Microsoft.WindowsAzure.Storage.Table;

namespace Messaging.Data.Repositories
{
    internal class MessageRepository : IMessageRepository
    {
        private const string RowKeyPrefix = "M;";
        private readonly CloudTable _table;

        public MessageRepository(StorageClientFactory clientFactory)
        {
            _table = clientFactory.GetTableClient()
                .GetTableReference("DataTable");
        }

        private async Task<MessageEntity> GetEntity(Guid appId, Guid channelId, Guid messageId)
        {
            var existingResult = await _table.ExecuteAsync(TableOperation.Retrieve<MessageEntity>(appId.ToString(), GetRowKey(channelId, messageId)));
            return (MessageEntity)existingResult.Result;
        }

        public async Task<Message> Get(Guid appId, Guid channelId, Guid messageId)
        {
            var message = await GetEntity(appId, channelId, messageId);

            return message == null ? null : ToDto(message);
        }

        public async Task<IEnumerable<Message>> GetAll(Guid appId, Guid channelId, int lastN)
        {
            var query = AzureTableHelper.GetRowKeyPrefixQuery<MessageEntity>(appId.ToString(), RowKeyPrefix + channelId);

            var entities = await AzureTableHelper.GetSegmentedResult(_table, query);

            IEnumerable<MessageEntity> orderedEntities = entities
                .OrderByDescending(message => message.CreatedAt);

            if (lastN > 0)
            {
                orderedEntities = orderedEntities.Take(lastN);
            }

            return orderedEntities
                .Select(ToDto)
                .ToList();
        }

        public async Task<Message> Upsert(Guid appId, Guid channelId, Message message)
        {
            var entity = new MessageEntity
            {
                PartitionKey = appId.ToString(),
                RowKey = GetRowKey(channelId, message.Id),
                Value = message.Value,
                CreatedAt = message.CreatedAt,
                CreatedBy = message.CreatedBy,
                UpdatedAt = message.CreatedAt,
                UpdatedBy = message.CreatedBy,
                CustomData = message.CustomData
            };
            var result = await _table.ExecuteAsync(TableOperation.InsertOrReplace(entity));
            var updated = (MessageEntity)result.Result;

            return ToDto(updated);
        }

        public async Task<bool> Delete(Guid appId, Guid channelId, Guid messageId)
        {
            var existing = await GetEntity(appId, channelId, messageId);
            if (existing == null)
                return false;

            await _table.ExecuteAsync(TableOperation.Delete(existing));

            return true;
        }

        private static string GetRowKey(Guid channelId, Guid messageId) => $"{RowKeyPrefix}{channelId};{messageId}";

        private static Message ToDto(MessageEntity entity) => new Message
        {
            Id = Guid.Parse(entity.RowKey.Split(';').Last()),
            Value = entity.Value,
            CreatedAt = entity.CreatedAt,
            CreatedBy = entity.CreatedBy,
            UpdatedAt = entity.UpdatedAt,
            UpdatedBy = entity.UpdatedBy,
            CustomData = entity.CustomData
        };
    }
}