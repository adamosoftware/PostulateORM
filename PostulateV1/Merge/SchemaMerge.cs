﻿using Postulate.Abstract;
using System;
using System.Collections.Generic;
using System.Linq;
using Postulate.Extensions;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.IO;
using Dapper;
using Postulate.Exceptions;

namespace Postulate.Merge
{
    public enum MergeActionType
    {
        Create,
        Alter,
        Rename,
        Drop
    }

    public enum MergeObjectType
    {
        Table,
        Column,
        Key,
        Index,
        ForeignKey
    }

    internal delegate IEnumerable<SchemaDiff> GetSchemaDiffMethod(IDbConnection connection);

    public partial class SchemaMerge<TDb, TKey> where TDb : SqlDb<TKey>, new()
    {
        private readonly IEnumerable<Type> _modelTypes;

        private const string _metaSchema = "meta";
        private const string _metaVersion = "Version";

        public SchemaMerge()
        {
            _modelTypes = typeof(TDb).Assembly.GetTypes()
                .Where(t =>
                    !t.Name.StartsWith("<>") &&         
                    t.Namespace.Equals(typeof(TDb).Namespace) &&
                    !t.HasAttribute<NotMappedAttribute>() &&
                    !t.IsAbstract &&
                    t.IsDerivedFromGeneric(typeof(Record<>)));
        }

        public IEnumerable<SchemaDiff> Compare(IDbConnection connection)
        {
            List<SchemaDiff> results = new List<SchemaDiff>();

            var diffMethods = new GetSchemaDiffMethod[]
            {
                // create
                CreateTablesAndColumns, CreatePrimaryKeys, CreateUniqueKeys, CreateIndexes, CreateForeignKeys,

                // alter
                AlterPrimaryKeys, AlterUniqueKeys, AlterIndexes, AlterNonKeyColumnTypes, AlterForeignKeys,

                // drop
                DropTables, DropNonKeyColumns, DropPrimaryKeys, DropUniqueKeys, DropIndexes
            };
            foreach (var method in diffMethods) results.AddRange(method.Invoke(connection));

            results.Add(ScriptVersionInfo(results));

            return results;
        }

        public IEnumerable<SchemaDiff> Compare()
        {            
            var db = new TDb();
            using (IDbConnection cn = db.GetConnection())
            {
                cn.Open();
                return Compare(cn);
            }            
        }

        public void SaveScriptAs(string fileName)
        {
            var db = new TDb();
            using (IDbConnection cn = db.GetConnection())
            {
                cn.Open();
                var diffs = Compare(cn);
                using (var file = File.CreateText(fileName))
                {
                    foreach (var diff in diffs)
                    {
                        foreach (var cmd in diff.SqlCommands(cn))
                        {
                            file.WriteLine(cmd);
                            file.WriteLine("\r\nGO\r\n");
                        }
                    }
                }
            }
        }

        public static bool Patch(Func<IEnumerable<SchemaDiff>, int, bool> uiAction = null)
        {
            int schemaVersion;
            var db = new TDb();
            using (IDbConnection cn = db.GetConnection())
            {
                cn.Open();
                if (IsPatchAvailable(cn, out schemaVersion))
                {
                    var sm = new SchemaMerge<TDb, TKey>();
                    var diffs = sm.Compare(cn);

                    if (uiAction != null)
                    {
                        // giving user opportunity to cancel           
                        if (!uiAction.Invoke(diffs, schemaVersion)) return false;
                    }

                    sm.Execute(cn, diffs);
                    return true;
                }
                return false;
            }
        }

        public void Execute(IDbConnection connection, IEnumerable<SchemaDiff> diffs)
        {
            if (diffs.Any(a => !a.IsValid(connection)))
            {
                string message = string.Join("\r\n", ValidationErrors(diffs));
                throw new ValidationException($"The model has one or more validation errors:\r\n{message}");
            }

            foreach (var diff in diffs)
            {
                foreach (var cmd in diff.SqlCommands(connection))
                {
                    // add to command queue somewhere
                    // enable setting command timeout?
                    connection.Execute(cmd);
                    // set some kind of success indicator somewhere
                }
            }
        }

        public void Execute()
        {
            var db = new TDb();
            using (IDbConnection cn = db.GetConnection())
            {
                cn.Open();
                var diffs = Compare(cn);
                Execute(cn, diffs);
            }
        }

        public static int GetSchemaVersion()
        {
            return (new TDb()).Version;
        }

        public static bool IsPatchAvailable(IDbConnection connection, out int schemaVersion)
        {
            int currentVersion = GetDbVersion(connection);
            schemaVersion = GetSchemaVersion();
            return (schemaVersion > currentVersion);
        }

        private static int GetDbVersion(IDbConnection connection)
        {
            CreateVersionTableIfNotExists(connection);
            return connection.QuerySingle<int?>("SELECT MAX([Value]) FROM [meta].[Version]", null) ?? 0;
        }

        private static void CreateVersionTableIfNotExists(IDbConnection connection)
        {
            if (!connection.Exists("[sys].[schemas] WHERE [name]=@name", new { name = _metaSchema })) connection.Execute($"CREATE SCHEMA [{_metaSchema}]");

            if (!connection.Exists("[sys].[tables] WHERE SCHEMA_NAME([schema_id])=@schema AND [name]=@name", new { schema = _metaSchema, name = _metaVersion }))
            {
                connection.Execute($@"CREATE TABLE [{_metaSchema}].[{_metaVersion}] (
					[Value] int NOT NULL,					
					CONSTRAINT [PK_{_metaSchema}_{_metaVersion}] PRIMARY KEY ([Value])
				)");
            }
        }

        public static bool IsSupportedType(Type type)
        {
            return
                CreateTable.SupportedTypes().ContainsKey(type) ||
                (type.IsEnum && type.GetEnumUnderlyingType().Equals(typeof(int))) ||
                (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>) && IsSupportedType(type.GetGenericArguments()[0]));
        }

        private static IEnumerable<ColumnRef> GetSchemaColumns(IDbConnection connection)
        {
            return connection.Query<ColumnRef>(
                @"SELECT SCHEMA_NAME([t].[schema_id]) AS [Schema], [t].[name] AS [TableName], [c].[Name] AS [ColumnName], 
					[t].[object_id] AS [ObjectID], TYPE_NAME([c].[system_type_id]) AS [DataType], 
					[c].[max_length] AS [ByteLength], [c].[is_nullable] AS [IsNullable],
					[c].[precision] AS [Precision], [c].[scale] as [Scale], [c].[collation_name] AS [Collation]
				FROM 
					[sys].[tables] [t] INNER JOIN [sys].[columns] [c] ON [t].[object_id]=[c].[object_id]", null);
        }

        private IEnumerable<ColumnRef> GetModelColumns(IEnumerable<Type> types, IDbConnection collationLookupConnection = null)
        {
            var results = types.SelectMany(t => t.GetProperties().Where(pi => !pi.HasAttribute<NotMappedAttribute>()).Select(pi => new ColumnRef(pi)));

            if (collationLookupConnection != null)
            {
                var collations = collationLookupConnection.Query<ColumnRef>(
                    @"SELECT 
	                    SCHEMA_NAME([tbl].[schema_id]) AS [Schema],
	                    [tbl].[name] AS [TableName],
	                    [col].[Name] AS [ColumnName],
	                    [col].[collation_name] AS [Collation]
                    FROM 
	                    [sys].[columns] [col] INNER JOIN [sys].[tables] [tbl] ON [col].[object_id]=[tbl].[object_id]
                    WHERE
	                    [col].[collation_name] IS NOT NULL");

                results = from cr in results
                          join col in collations on
                            new { Schema = cr.Schema, TableName = cr.TableName, ColumnName = cr.ColumnName } equals
                            new { Schema = col.Schema, TableName = col.TableName, ColumnName = col.ColumnName } into collatedColumns
                          from output in collatedColumns.DefaultIfEmpty()
                          select new ColumnRef(cr.PropertyInfo) { Collation = output?.Collation };
            }

            return results;
        }

        private static IEnumerable<DbObject> GetSchemaTables(IDbConnection connection)
        {
            return connection.Query<DbObject>(
                @"SELECT 
                    SCHEMA_NAME([t].[schema_id]) AS [Schema], [t].[name] AS [TableName], [t].[object_id] AS [ObjectId]
                FROM 
                    [sys].[tables] [t]");
        }
    }
}