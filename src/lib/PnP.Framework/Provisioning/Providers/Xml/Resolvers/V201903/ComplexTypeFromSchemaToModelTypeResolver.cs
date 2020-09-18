﻿using PnP.Framework.Extensions;
using System;
using System.Collections.Generic;

namespace PnP.Framework.Provisioning.Providers.Xml.Resolvers
{
    /// <summary>
    /// Type resolver for complex types from Schema to Model
    /// </summary>
    internal class ComplexTypeFromSchemaToModelTypeResolver<TargetType> : ITypeResolver
        where TargetType : new()
    {
        public string Name => this.GetType().Name;
        public bool CustomCollectionResolver => false;

        private readonly String sourcePropertyName;

        public ComplexTypeFromSchemaToModelTypeResolver(String sourcePropertyName)
        {
            this.sourcePropertyName = sourcePropertyName;
        }

        public object Resolve(object source, Dictionary<string, IResolver> resolvers = null, bool recursive = false)
        {
            TargetType result = default(TargetType);

            var sourceProperty = source.GetPublicInstancePropertyValue(this.sourcePropertyName);

            if (null != sourceProperty)
            {
                result = new TargetType();
                PnPObjectsMapper.MapProperties(sourceProperty, result, resolvers, recursive);
            }

            return (result);
        }
    }
}
