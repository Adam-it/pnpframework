﻿using System;
using System.Threading;

namespace PnP.Framework.Provisioning.Providers.Xml
{
    /// <summary>
    /// Internal class to handle a Provisioning Template serialization scope
    /// </summary>
    internal class PnPSerializationScope : IDisposable
    {
        private readonly String _baseSchemaNamespace;
        private readonly String _baseSchemaAssemblyName;
        private readonly PnPSerializationScope _previous;

        public String BaseSchemaNamespace => this._baseSchemaNamespace;
        public String BaseSchemaAssemblyName => this._baseSchemaAssemblyName;

        public PnPSerializationScope(Type schemaTemplateType)
        {
            // Save the scope information
            this._baseSchemaNamespace = schemaTemplateType.Namespace;
            this._baseSchemaAssemblyName = schemaTemplateType.Assembly.FullName;

            // Save the previous scope, if any
            this._previous = Current;

            // Set the new scope to this
            Current = this;
        }

        ~PnPSerializationScope()
        {
            Dispose(false);
        }

        /// <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                Current = this._previous;
            }
        }

        private static readonly AsyncLocal<PnPSerializationScope> _pnpSerializationScope = new AsyncLocal<PnPSerializationScope>();

        public static PnPSerializationScope Current
        {
            get { return _pnpSerializationScope.Value; }
            set
            {
                _pnpSerializationScope.Value = value;
            }
        }
    }
}
