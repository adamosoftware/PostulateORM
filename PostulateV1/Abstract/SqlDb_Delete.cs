﻿using Dapper;
using Postulate.Orm.Interfaces;
using System.Data;
using ReflectionHelper;
using Postulate.Orm.Attributes;
using Postulate.Orm.Exceptions;

namespace Postulate.Orm.Abstract
{
    public abstract partial class SqlDb<TKey> : IDb
    {
        protected abstract void OnCaptureDeletion<TRecord>(IDbConnection connection, TRecord record, IDbTransaction transasction) where TRecord : Record<TKey>;

        protected abstract TRecord BeginRestore<TRecord>(IDbConnection connection, TKey id) where TRecord : Record<TKey>;

        protected abstract void CompleteRestore<TRecord>(IDbConnection connection, TKey id, IDbTransaction transaction) where TRecord : Record<TKey>;

        public void DeleteOne<TRecord>(IDbConnection connection, TKey id) where TRecord : Record<TKey>
        {
            TRecord record = Find<TRecord>(connection, id);
            if (record != null) DeleteOne(connection, record);
        }

        public void DeleteOne<TRecord>(IDbConnection connection, TRecord record) where TRecord : Record<TKey>
        {
            string message;
            if (record.AllowDelete(connection, UserName, out message))
            {
                if (TrackDeletions<TRecord>())
                {
                    using (var txn = GetTransaction(connection))
                    {
                        try
                        {
                            OnCaptureDeletion(connection, record, txn);
                            ExecuteDeleteOne<TRecord>(connection, record.Id, txn);
                            txn.Commit();
                        }
                        catch
                        {
                            txn.Rollback();
                            throw;
                        }
                    }
                }
                else
                {
                    ExecuteDeleteOne<TRecord>(connection, record.Id);
                }

                record.AfterDelete(connection);
            }
            else
            {
                throw new PermissionDeniedException(message);
            }
        }

        public void DeleteOneWhere<TRecord>(IDbConnection connection, string criteria, object parameters) where TRecord : Record<TKey>
        {
            TRecord record = FindWhere<TRecord>(connection, criteria, parameters);
            if (record != null) DeleteOne<TRecord>(connection, record.Id);
        }

        public int DeleteAllWhere<TRecord>(IDbConnection connection, string criteria, object parameters) where TRecord : Record<TKey>
        {
            string cmd = $"DELETE {GetTableName<TRecord>()} WHERE {criteria}";

            if (TrackDeletions<TRecord>())
            {
                var records = connection.Query<TRecord>($"SELECT * FROM {GetTableName<TRecord>()} WHERE {criteria}", parameters);

                using (var txn = GetTransaction(connection))
                {
                    try
                    {
                        foreach (var record in records) OnCaptureDeletion(connection, record, txn);
                        int result = connection.Execute(cmd, parameters, txn);
                        txn.Commit();
                        return result;
                    }
                    catch
                    {
                        txn.Rollback();
                        throw;
                    }
                }
            }
            else
            {
                return connection.Execute(cmd, parameters);
            }
        }        

        public TKey RestoreOne<TRecord>(IDbConnection connection, TKey id) where TRecord : Record<TKey>
        {
            var record = BeginRestore<TRecord>(connection, id);

            using (var txn = GetTransaction(connection))
            {
                try
                {
                    TKey newId = ExecuteInsert(connection, record, txn);
                    CompleteRestore<TRecord>(connection, id, txn);
                    txn.Commit();
                    return newId;
                }
                catch
                {
                    txn.Rollback();
                    throw;
                }
            }
        }

        private void ExecuteDeleteOne<TRecord>(IDbConnection connection, TKey id, IDbTransaction txn = null) where TRecord : Record<TKey>
        {
            string cmd = GetCommand<TRecord>(_deleteCommands, () => GetDeleteStatement<TRecord>());
            connection.Execute(cmd, new { id = id }, txn);
        }

        private bool TrackDeletions<TRecord>() where TRecord : Record<TKey>
        {
            return typeof(TRecord).HasAttribute<TrackDeletionsAttribute>();
        }
    }
}
