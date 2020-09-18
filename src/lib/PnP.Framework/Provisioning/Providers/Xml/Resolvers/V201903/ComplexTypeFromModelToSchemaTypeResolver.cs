﻿using PnP.Framework.Extensions;
using System;
using System.Collections.Generic;

namespace PnP.Framework.Provisioning.Providers.Xml.Resolvers
{
    /// <summary>
    /// Type resolver for complex types from Model to Schema
    /// </summary>
    internal class ComplexTypeFromModelToSchemaTypeResolver : ITypeResolver
    {
        public string Name => this.GetType().Name;
        public bool CustomCollectionResolver => false;

        private readonly String sourcePropertyName;
        private readonly Type destinationType;

        public ComplexTypeFromModelToSchemaTypeResolver(Type destinationType, String sourcePropertyName)
        {
            this.destinationType = destinationType;
            this.sourcePropertyName = sourcePropertyName;
        }

        public object Resolve(object source, Dictionary<string, IResolver> resolvers = null, bool recursive = false)
        {
            Object result = null;

            var sourceProperty = source.GetPublicInstancePropertyValue(this.sourcePropertyName);

            if (null != sourceProperty)
            {
                result = Activator.CreateInstance(this.destinationType);
                PnPObjectsMapper.MapProperties(sourceProperty, result, resolvers, recursive);
            }

            return (result);
        }
    }
}
