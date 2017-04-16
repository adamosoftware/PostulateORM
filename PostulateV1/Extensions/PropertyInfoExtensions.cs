﻿using Postulate.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Postulate.Extensions
{
    public static class PropertyInfoExtensions
    {
        public static bool HasColumnAccess(this ICustomAttributeProvider provider, Access access)
        {
            return !provider.HasAttribute<ColumnAccessAttribute>() || provider.HasAttribute<ColumnAccessAttribute>(attr => attr.Access == access);
        }

        public static bool HasAttribute<TAttribute>(this ICustomAttributeProvider provider, out TAttribute attribute, Func<TAttribute, bool> predicate = null) where TAttribute : Attribute
        {
            attribute = null;            
            var attrs = provider.GetCustomAttributes(typeof(TAttribute), true).OfType<TAttribute>();

            if (predicate != null) attrs = attrs.Where(a => predicate.Invoke(a));

            if (attrs.Any())
            {
                attribute = attrs.First() as TAttribute;
                return true;
            }
            return false;
        }

        public static bool HasAttribute<TAttribute>(this ICustomAttributeProvider provider, Func<TAttribute, bool> predicate = null) where TAttribute : Attribute
        {
            TAttribute attr;
            return HasAttribute(provider, out attr, predicate);
        }

    }
}
