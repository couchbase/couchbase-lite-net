using System;
using System.Collections.Generic;
using System.Text;
using Couchbase.Lite.Serialization;
using Newtonsoft.Json;

namespace Couchbase.Lite.DB
{
    [JsonConverter(typeof(IJsonMappedConverter))]
    internal sealed class Subdocument : PropertyContainer, ISubdocument, IJsonMapped
    {
        private Action _onMutate;

        protected internal override IBlob CreateBlob(IDictionary<string, object> properties)
        {
            var doc = Document;
            if (doc != null) {
                return new Blob(doc.Database as Database, properties);
            }

            throw new InvalidOperationException("Cannot read blob inside a subdocument not attached to a document");
        }

        public IDocument Document
        {
            get {
                var p = Parent;
                return (p is Document) ? (IDocument)p : (IDocument)((Subdocument)p)?.Parent;
            }
        }

        public bool Exists
        {
            get {
                AssertSafety();
                return HasRoot();
            }
        }

        internal override bool HasChanges
        {
            get {
                return base.HasChanges;
            }
            set {
                if (base.HasChanges == value) {
                    return;
                }

                base.HasChanges = value;
                _onMutate?.Invoke();
            }
        }

        internal string Key { get; set; }

        internal PropertyContainer Parent { get; set; }

        internal Subdocument()
            : base(new SharedStringCache())
        {
            
        }

        internal Subdocument(PropertyContainer parent, SharedStringCache sharedKeys)
            : base(sharedKeys)
        {
            Parent = parent;
        }

        internal void SetOnMutate(Action onMutate)
        {
            _onMutate = onMutate;
        }

        internal unsafe void Invalidate()
        {
            Parent = null;
            SetOnMutate(null);
            SetRootDict(null);
            Properties = null;
            ResetChangesKeys();
        }

        public void WriteTo(IJsonWriter writer)
        {
            foreach(var prop in Properties) {
                writer.Write(prop.Key, prop.Value);
            }
        }
    }
}

