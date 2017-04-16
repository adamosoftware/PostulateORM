﻿using Postulate.Abstract;
using Postulate.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Postulate.Extensions
{
    public static class TypeExtensions
    {
        public static string IdentityColumnName(this Type type)
        {
            string result = SqlDb.IdentityColumnName;

            IdentityColumnAttribute attr;
            if (type.HasAttribute(out attr)) result = attr.ColumnName;

            return result;
        }

    }
}
