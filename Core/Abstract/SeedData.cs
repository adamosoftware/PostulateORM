﻿using Dapper;
using Postulate.Orm.Extensions;
using System.Collections.Generic;
using System.Data;

namespace Postulate.Orm.Abstract
{
    public abstract class SeedData<TRecord, TKey> where TRecord : Record<TKey>, new()
    {
        public abstract string ExistsCriteria { get; }
        public abstract IEnumerable<TRecord> Records { get; }

        public void Generate(IDbConnection connection, SqlDb<TKey> db)
        {
            foreach (var record in Records)
            {
                var existingRecord = connection.QuerySingleOrDefault<TRecord>($"SELECT * FROM {ExistsCriteria}", record);
                // this will cause the existing seed record to be updated instead of inserted
                if (existingRecord != null) record.Id = existingRecord.Id;                
                db.Save(connection, record);
            }
        }

        public void Generate(SqlDb<TKey> db)
        {
            using (var cn = db.GetConnection())
            {
                cn.Open();
                Generate(cn, db);
            }
        }
    }
}