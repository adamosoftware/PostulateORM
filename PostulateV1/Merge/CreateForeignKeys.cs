﻿using Postulate.Abstract;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postulate.Merge
{
    public partial class SchemaMerge<TDb, TKey> where TDb : SqlDb<TKey>, new()
    {
        /// <summary>
        /// Creates foreign keys that exist in the model but not in the schema
        /// </summary>
        private IEnumerable<SchemaDiff> CreateForeignKeys(IDbConnection connection)
        {
            throw new NotImplementedException();
        }
    }
}
