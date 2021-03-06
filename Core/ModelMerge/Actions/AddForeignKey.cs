﻿using Postulate.Orm.Abstract;
using Postulate.Orm.Attributes;
using Postulate.Orm.Extensions;
using System.Collections.Generic;
using System.Data;
using System.Reflection;

namespace Postulate.Orm.ModelMerge.Actions
{
	public class AddForeignKey : MergeAction
	{
		private readonly PropertyInfo _propertyInfo;

		public AddForeignKey(SqlSyntax syntax, PropertyInfo propertyInfo) : base(syntax, ObjectType.ForeignKey, ActionType.Create, $"{propertyInfo.QualifiedName()}")
		{
			_propertyInfo = propertyInfo;
		}

		public override IEnumerable<string> SqlCommands(IDbConnection connection)
		{
			yield return Syntax.ForeignKeyAddStatement(_propertyInfo);

			ForeignKeyAttribute fk = _propertyInfo.GetForeignKeyAttribute();

			if (fk != null && fk.CreateIndex && !Syntax.IndexExists(connection, _propertyInfo.IndexName(Syntax)))
			{
				yield return Syntax.CreateColumnIndexStatement(_propertyInfo);
			}
		}
	}
}