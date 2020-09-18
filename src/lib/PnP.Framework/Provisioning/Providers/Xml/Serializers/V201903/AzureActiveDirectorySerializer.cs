﻿using PnP.Framework.Extensions;
using PnP.Framework.Provisioning.Model;
using PnP.Framework.Provisioning.Model.AzureActiveDirectory;
using PnP.Framework.Provisioning.Providers.Xml.Resolvers;
using PnP.Framework.Utilities;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Security;

namespace PnP.Framework.Provisioning.Providers.Xml.Serializers
{
    /// <summary>
    /// Class to serialize/deserialize the AAD settings
    /// </summary>
    [TemplateSchemaSerializer(
        MinimalSupportedSchemaVersion = XMLPnPSchemaVersion.V201903,
        SerializationSequence = 200, DeserializationSequence = 200,
        Scope = SerializerScope.Tenant)]
    internal class AzureActiveDirectorySerializer : PnPBaseSchemaSerializer<ProvisioningAzureActiveDirectory>
    {
        public override void Deserialize(object persistence, ProvisioningTemplate template)
        {
            var aad = persistence.GetPublicInstancePropertyValue("AzureActiveDirectory");

            if (aad != null)
            {
                var expressions = new Dictionary<Expression<Func<ProvisioningAzureActiveDirectory, Object>>, IResolver>
                {

                    // Manage the Users and their Password Profile
                    { a => a.Users, new AADUsersFromSchemaToModelTypeResolver() },
                    { a => a.Users[0].PasswordProfile, new AADUsersPasswordProfileFromSchemaToModelTypeResolver() },
                    {
                        a => a.Users[0].PasswordProfile.Password,
                        new ExpressionValueResolver((s, p) => EncryptionUtility.ToSecureString((String)p))
                    },

                    // Manage licenses for users
                    {
                        a => a.Users[0].Licenses[0].DisabledPlans,
                        new ExpressionValueResolver((s, p) => s.GetPublicInstancePropertyValue("DisabledPlans"))
                    }
                };

                PnPObjectsMapper.MapProperties(aad, template.ParentHierarchy.AzureActiveDirectory, expressions, recursive: true);
            }
        }

        public override void Serialize(ProvisioningTemplate template, object persistence)
        {
            if (template.ParentHierarchy?.AzureActiveDirectory?.Users != null)
            {
                var aadTypeName = $"{PnPSerializationScope.Current?.BaseSchemaNamespace}.AzureActiveDirectory, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
                var aadType = Type.GetType(aadTypeName, false);
                var aadUserTypeName = $"{PnPSerializationScope.Current?.BaseSchemaNamespace}.AADUsersUser, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
                var aadUserType = Type.GetType(aadUserTypeName, false);
                var aadUserPasswordProfileTypeName = $"{PnPSerializationScope.Current?.BaseSchemaNamespace}.AADUsersUserPasswordProfile, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
                var aadUserPasswordProfileType = Type.GetType(aadUserPasswordProfileTypeName, false);
                var aadUserLicenseProfileTypeName = $"{PnPSerializationScope.Current?.BaseSchemaNamespace}.AADUsersUserLicense, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
                var aadUserLicenseProfileType = Type.GetType(aadUserLicenseProfileTypeName, false);

                if (aadType != null &&
                    aadUserType != null &&
                    aadUserPasswordProfileType != null)
                {
                    var target = Activator.CreateInstance(aadType, true);

                    var resolvers = new Dictionary<String, IResolver>
                    {
                        {
                            $"{aadType}.Users",
                            new AADUsersFromModelToSchemaTypeResolver()
                        },
                        {
                            $"{aadUserType}.PasswordProfile",
                            new AADUsersPasswordProfileFromModelToSchemaTypeResolver()
                        },
                        {
                            $"{aadUserPasswordProfileType}.Password",
                            new ExpressionValueResolver((s, p) => EncryptionUtility.ToInsecureString((SecureString)p))
                        },
                        {
                            $"{aadUserLicenseProfileType}.DisabledPlans",
                            new ExpressionValueResolver((s, p) => ((UserLicense)s).DisabledPlans)
                        }
                    };

                    PnPObjectsMapper.MapProperties(template.ParentHierarchy.AzureActiveDirectory, target, resolvers, recursive: true);

                    if (target != null &&
                        target.GetPublicInstancePropertyValue("Users") != null)
                    {
                        persistence.GetPublicInstanceProperty("AzureActiveDirectory").SetValue(persistence, target);
                    }
                }
            }
        }
    }
}
