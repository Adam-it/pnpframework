﻿using System;

namespace PnP.Framework.Provisioning.Connectors.OpenXML
{
    /// <summary>
    /// Defines a single file in the PnP Open XML file package
    /// </summary>
    public class PnPPackageFileItem
    {
        /// <summary>
        /// Name of the package file item
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// Folder containing the package file item
        /// </summary>
        public string Folder { get; set; }
        /// <summary>
        /// Content of the package file item
        /// </summary>
        public byte[] Content { get; set; }
    }
}
