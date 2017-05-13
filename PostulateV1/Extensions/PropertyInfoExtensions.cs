﻿using Postulate.Attributes;
using Postulate.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ReflectionHelper;

namespace Postulate.Extensions
{
    public static class PropertyInfoExtensions
    {
        public static bool HasColumnAccess(this ICustomAttributeProvider provider, Access access)
        {
            return !provider.HasAttribute<ColumnAccessAttribute>() || provider.HasAttribute<ColumnAccessAttribute>(attr => attr.Access == access);
        }

        public static bool IsForSaveAction(this ICustomAttributeProvider provider, SaveAction action)
        {
            Dictionary<SaveAction, Access> map = new Dictionary<SaveAction, Access>()
            {
                { SaveAction.Insert, Access.InsertOnly },
                { SaveAction.Update, Access.UpdateOnly }
            };
            return HasColumnAccess(provider, map[action]);
        }
    }
}
