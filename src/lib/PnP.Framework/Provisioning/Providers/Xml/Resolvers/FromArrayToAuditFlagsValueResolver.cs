﻿using Microsoft.SharePoint.Client;
using PnP.Framework.Extensions;
using System;
using System.Collections;

namespace PnP.Framework.Provisioning.Providers.Xml.Resolvers
{
    /// <summary>
    /// Resolves an array of Strings into an enum bit mask of AuditFlags
    /// </summary>
    internal class FromArrayToAuditFlagsValueResolver : IValueResolver
    {
        public string Name => this.GetType().Name;

        public object Resolve(object source, object destination, object sourceValue)
        {
            AuditMaskType auditMask = AuditMaskType.None;
            var audits = source.GetPublicInstancePropertyValue("Audit");
            if (audits != null)
            {
                foreach (var a in (IEnumerable)audits)
                {
                    auditMask |= (AuditMaskType)Enum.Parse(typeof(AuditMaskType),
                        a.GetPublicInstancePropertyValue("AuditFlag").ToString());
                }
            }
            return auditMask;
        }
    }
}
