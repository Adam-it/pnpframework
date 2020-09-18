﻿using PnP.Framework.Extensions;
using PnP.Framework.Provisioning.Model;
using System;
using System.Linq;
using System.Xml.Linq;

namespace PnP.Framework.Provisioning.Providers.Xml.Serializers
{
    /// <summary>
    /// Class to serialize/deserialize the Site Columns
    /// </summary>
    [TemplateSchemaSerializer(
        MinimalSupportedSchemaVersion = XMLPnPSchemaVersion.V201605,
        SerializationSequence = 900, DeserializationSequence = 900,
        Scope = SerializerScope.ProvisioningTemplate)]
    internal class SiteColumnsSerializer : PnPBaseSchemaSerializer<Field>
    {
        public override void Deserialize(object persistence, ProvisioningTemplate template)
        {
            var siteFields = persistence.GetPublicInstancePropertyValue("SiteFields");
            var fields = siteFields.GetPublicInstancePropertyValue("Any") as System.Xml.XmlElement[];

            if (fields != null)
            {
                foreach (var f in fields)
                {
                    template.SiteFields.Add(new Field { SchemaXml = f.OuterXml });
                }
            }
        }

        public override void Serialize(ProvisioningTemplate template, object persistence)
        {
            if (template.SiteFields != null && template.SiteFields.Count > 0)
            {
                var fieldsTypeName = $"{PnPSerializationScope.Current?.BaseSchemaNamespace}.ProvisioningTemplateSiteFields, {PnPSerializationScope.Current?.BaseSchemaAssemblyName}";
                var fieldsType = Type.GetType(fieldsTypeName, true);
                var fields = Activator.CreateInstance(fieldsType);

                var xmlFields = from f in template.SiteFields
                                select XElement.Parse(f.SchemaXml).ToXmlElement();

                fields.SetPublicInstancePropertyValue("Any", xmlFields.ToArray());

                if (fields != null && ((Array)fields.GetPublicInstancePropertyValue("Any")).Length > 0)
                {
                    persistence.SetPublicInstancePropertyValue("SiteFields", fields);
                }
            }
        }
    }
}
