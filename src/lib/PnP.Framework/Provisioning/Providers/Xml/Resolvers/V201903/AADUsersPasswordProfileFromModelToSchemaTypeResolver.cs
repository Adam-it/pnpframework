﻿using System;
using System.Collections.Generic;
using AAD = PnP.Framework.Provisioning.Model.AzureActiveDirectory;

namespace PnP.Framework.Provisioning.Providers.Xml.Resolvers
{
    /// <summary>
    /// Resolves the AAD Users from the Model to the Schema
    /// </summary>
    internal class AADUsersPasswordProfileFromModelToSchemaTypeResolver : ITypeResolver
    {
        public string Name => this.GetType().Name;

        public bool CustomCollectionResolver => false;

        public object Resolve(object source, Dictionary<String, IResolver> resolvers = null, Boolean recursive = false)
        {
            var user = source as AAD.User;
            var passwordProfile = user?.PasswordProfile;

            var passwordProfileTypeName = $"{PnPSerializationScope.Current?.BaseSchemaNamespace}.AADUsersUserPasswordProfile, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
            var passwordProfileType = Type.GetType(passwordProfileTypeName, true);

            var result = Activator.CreateInstance(passwordProfileType);

            if (null != passwordProfile)
            {
                PnPObjectsMapper.MapProperties(passwordProfile, result, resolvers, recursive);
            }

            return (result);
        }
    }
}
