﻿using Postulate.Orm.Abstract;
using Postulate.Orm.Models;
using System.Collections.Generic;
using System.Data;

namespace Postulate.Orm.ModelMerge.Actions
{
    public class DropColumn : MergeAction
    {
        private readonly ColumnInfo _columnInfo;

        public DropColumn(SqlSyntax syntax, ColumnInfo columnInfo) : base(syntax, ObjectType.Column, ActionType.Drop, $"{columnInfo}")
        {
            _columnInfo = columnInfo;
        }

        public override IEnumerable<string> SqlCommands(IDbConnection connection)
        {
            string constrainName;
            bool clustered;
            bool inPK = Syntax.IsColumnInPrimaryKey(connection, _columnInfo, out clustered, out constrainName);
            var foreignKeys = Syntax.GetDependentForeignKeys(connection, _columnInfo.GetTableInfo()); ;

            if (inPK)
            {                
                foreach (var fk in foreignKeys) yield return Syntax.ForeignKeyDropStatement(fk);

                yield return Syntax.PrimaryKeyDropStatement(_columnInfo.GetTableInfo(), constrainName);
            }

            yield return Syntax.ColumnDropStatement(_columnInfo);            

            if (inPK)
            {
                yield return Syntax.PrimaryKeyAddStatement(_columnInfo.GetTableInfo());

                foreach (var fk in foreignKeys) yield return Syntax.ForeignKeyAddStatement(fk);
            }
        }
    }
}