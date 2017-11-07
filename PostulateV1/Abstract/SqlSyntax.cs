﻿using Dapper;
using Postulate.Orm.Extensions;
using Postulate.Orm.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;

namespace Postulate.Orm.Abstract
{
    public abstract class SqlSyntax
    {
        public abstract string CommentPrefix { get; }

        public abstract string CommandSeparator { get; }

        public abstract string ApplyDelimiter(string objectName);

        public abstract string GetTableName(Type type);       

        public abstract string IsTableEmptyQuery { get; }

        public abstract string TableExistsQuery { get; }

        public abstract object TableExistsParameters(Type type);

        public abstract string ColumnExistsQuery { get; }

        public abstract object ColumnExistsParameters(PropertyInfo propertyInfo);

        public abstract string SchemaColumnQuery { get; }

        public abstract object SchemaColumnParameters(Type type);

        public abstract void FindObjectId(IDbConnection connection, TableInfo tableInfo);

        public abstract string SqlDataType(PropertyInfo propertyInfo);

        public bool IsTableEmpty(IDbConnection connection, Type t)
        {
            //$"SELECT COUNT(1) FROM [{schema}].[{tableName}]"
            return ((connection.QueryFirstOrDefault<int?>(IsTableEmptyQuery, null) ?? 0) == 0);
        }

        public bool TableExists(IDbConnection connection, Type t)
        {
            //return connection.Exists("[sys].[tables] WHERE SCHEMA_NAME([schema_id])=@schema AND [name]=@name", new { schema = schema, name = tableName });
            return connection.Exists(TableExistsQuery, TableExistsParameters(t));
        }

        public bool ColumnExists(IDbConnection connection, PropertyInfo pi)
        {
            return connection.Exists(ColumnExistsQuery, ColumnExistsParameters(pi));
        }

        public abstract ILookup<int, ColumnInfo> GetSchemaColumns(IDbConnection connection);

        public abstract IEnumerable<TableInfo> GetSchemaTables(IDbConnection connection);

        protected abstract string GetExcludeSchemas(IDbConnection connection);

        public abstract IEnumerable<ForeignKeyInfo> GetDependentForeignKeys(IDbConnection connection, TableInfo tableInfo);

        public abstract Dictionary<Type, string> KeyTypeMap(bool withDefaults = true);

        public abstract Dictionary<Type, string> SupportedTypes(string length = null, byte precision = 0, byte scale = 0);

        public bool IsSupportedType(Type type)
        {
            return SupportedTypes().ContainsKey(type);
        }

        public abstract string GetColumnSyntax(PropertyInfo propertyInfo, bool forceNull = false);

        public abstract string GetColumnType(PropertyInfo propertyInfo, bool forceNull = false);

        public abstract string GetColumnDefault(PropertyInfo propertyInfo, bool forCreateTable = false);

        public abstract string GetConstraintBaseName(Type type);

        public abstract string GetDropForeignKeyStatement(ForeignKeyInfo foreignKeyInfo);

        public abstract string GetDropTableStatement(TableInfo tableInfo);

        public abstract IEnumerable<KeyColumnInfo> GetKeyColumns(IDbConnection connection, Func<KeyColumnInfo, bool> filter = null);

        public abstract string GetCopyStatement<TRecord, TKey>(IEnumerable<string> paramColumns, IEnumerable<string> columns) where TRecord : Record<TKey>;
    }
}