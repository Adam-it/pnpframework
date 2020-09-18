﻿using PnP.Framework.Extensions;
using System;
using System.Collections.Generic;

namespace PnP.Framework.Provisioning.Providers.Xml.Resolvers
{
    /// <summary>
    /// Resolves a Document Template of a Content Type from Domain Model to Schema
    /// </summary>
    internal class DocumentTemplateFromModelToSchemaTypeResolver : ITypeResolver
    {
        public string Name => this.GetType().Name;

        public bool CustomCollectionResolver => false;

        private readonly Type _targetType;

        public DocumentTemplateFromModelToSchemaTypeResolver(Type targetType)
        {
            this._targetType = targetType;
        }

        public object Resolve(object source, Dictionary<String, IResolver> resolvers = null, Boolean recursive = false)
        {
            Object result = null;
            Model.ContentType contentType = source as Model.ContentType;

            if (null != contentType && !String.IsNullOrEmpty(contentType.DocumentTemplate))
            {
                result = Activator.CreateInstance(this._targetType);
                result.SetPublicInstancePropertyValue("TargetName", contentType.DocumentTemplate);
            }

            return (result);
        }
    }
}
