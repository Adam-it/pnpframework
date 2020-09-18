﻿using Microsoft.SharePoint.Client;
using PnP.Framework.Provisioning.Model;
using PnP.Framework.Provisioning.ObjectHandlers.Extensions;

namespace PnP.Framework.Provisioning.ObjectHandlers
{
    internal class ObjectLocalization : ObjectHandlerBase
    {
        public override string Name
        {
            get
            {
                return "Localization";
            }
        }

        public override string InternalName => "Localization";

        public override ProvisioningTemplate ExtractObjects(Web web, ProvisioningTemplate template, ProvisioningTemplateCreationInformation creationInfo)
        {
            if (creationInfo.PersistMultiLanguageResources)
            {
                template = UserResourceExtensions.SaveResourceValues(template, creationInfo);
            }
            return template;
        }

        public override TokenParser ProvisionObjects(Web web, ProvisioningTemplate template, TokenParser parser, ProvisioningTemplateApplyingInformation applyingInformation)
        {
            return parser;
        }

        public override bool WillExtract(Web web, ProvisioningTemplate template, ProvisioningTemplateCreationInformation creationInfo)
        {
            return creationInfo.PersistMultiLanguageResources;
        }

        public override bool WillProvision(Web web, ProvisioningTemplate template, ProvisioningTemplateApplyingInformation applyingInformation)
        {
            return false;
        }
    }
}
