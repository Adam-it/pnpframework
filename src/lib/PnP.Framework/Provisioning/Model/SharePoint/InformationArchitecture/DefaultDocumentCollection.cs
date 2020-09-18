﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PnP.Framework.Provisioning.Model
{
    public partial class DefaultDocumentCollection : BaseProvisioningTemplateObjectCollection<DefaultDocument>
    {
        /// <summary>
        /// Constructor for DefaultDocumentCollection class.
        /// </summary>
        /// <param name="parentTemplate">Parent provisioning template</param>
        public DefaultDocumentCollection(ProvisioningTemplate parentTemplate) : base(parentTemplate)
        {

        }
    }
}
