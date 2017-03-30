using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite.DB;

namespace Couchbase.Lite
{
    internal interface IModellable
    {
        T AsModel<T>() where T : IDocumentModel, new();
    }

    internal static class IDocumentModelExtensions
    {
        internal static void Save(this IDocumentModel model)
        {
            var document = model.Document as Document;
            if (document == null) {
                throw new NotSupportedException("Custom IDocument not supported");
            }

            document.Set(model);
        }
    }
}
