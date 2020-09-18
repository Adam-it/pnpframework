﻿using PnP.Framework.Extensions;
using PnP.Framework.Provisioning.Model;
using System;

namespace PnP.Framework.Provisioning.Providers.Xml.Serializers
{
    /// <summary>
    /// Class to serialize/deserialize the Site Header
    /// </summary>
    [TemplateSchemaSerializer(
        MinimalSupportedSchemaVersion = XMLPnPSchemaVersion.V201903,
        SerializationSequence = 810, DeserializationSequence = 810,
        Scope = SerializerScope.ProvisioningTemplate)]
    internal class SiteHeaderSerializer : PnPBaseSchemaSerializer<SiteHeader>
    {
        public override void Deserialize(object persistence, ProvisioningTemplate template)
        {
            var siteHeader = persistence.GetPublicInstancePropertyValue("Header");

            if (siteHeader != null)
            {
                template.Header = new SiteHeader();
                PnPObjectsMapper.MapProperties(siteHeader, template.Header, null, true);
            }
        }

        public override void Serialize(ProvisioningTemplate template, object persistence)
        {
            if (template.Header != null)
            {
                var siteHeaderType = Type.GetType($"{PnPSerializationScope.Current?.BaseSchemaNamespace}.Header, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}", true);
                var target = Activator.CreateInstance(siteHeaderType, true);

                PnPObjectsMapper.MapProperties(template.Header, target, null, recursive: true);

                persistence.GetPublicInstanceProperty("Header").SetValue(persistence, target);
            }
        }
    }
}
