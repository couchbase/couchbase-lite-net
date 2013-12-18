
namespace Sharpen
{
	using System;
	using System.Collections.Generic;
	using System.Linq;

	internal class LinkedHashMap<T, U> : AbstractMap<T, U>
	{
        internal List<KeyValuePair<T, U>> List { get; private set; }
        internal Dictionary<T, U> Table { get; private set; }

        public LinkedHashMap ()
        {
            this.Table = new Dictionary<T, U> ();
            this.List = new List<KeyValuePair<T, U>> ();
        }

        public LinkedHashMap (Int32 initialCapacity)
        {
            this.Table = new Dictionary<T, U> ();
            this.List = new List<KeyValuePair<T, U>> (initialCapacity);
        }

        public LinkedHashMap (Int32 initialCapacity, Single loadFactor, Boolean accessOrder) : this(initialCapacity) { }

        public LinkedHashMap (LinkedHashMap<T, U> map)
        {
            this.Table = map.Table;
            this.List = map.List;
        }

		public override void Clear ()
		{
			Table.Clear ();
			List.Clear ();
		}
		
		public override int Count {
			get {
				return List.Count;
			}
		}
		
		public override bool ContainsKey (object name)
		{
			return Table.ContainsKey ((T)name);
		}

		public override ICollection<KeyValuePair<T, U>> EntrySet ()
		{
			return this;
		}

		public override U Get (object key)
		{
			U local;
			Table.TryGetValue ((T)key, out local);
			return local;
		}

		protected override IEnumerator<KeyValuePair<T, U>> InternalGetEnumerator ()
		{
			return List.GetEnumerator ();
		}

		public override bool IsEmpty ()
		{
			return (Table.Count == 0);
		}

		public override U Put (T key, U value)
		{
			U old;
			if (Table.TryGetValue (key, out old)) {
				int index = List.FindIndex (p => p.Key.Equals (key));
				if (index != -1)
					List.RemoveAt (index);
			}
			Table[key] = value;
			List.Add (new KeyValuePair<T, U> (key, value));
			return old;
		}

		public override U Remove (object key)
		{
			U local = default(U);
			if (Table.TryGetValue ((T)key, out local)) {
				int index = List.FindIndex (p => p.Key.Equals (key));
				if (index != -1)
					List.RemoveAt (index);
				Table.Remove ((T)key);
			}
			return local;
		}

		public override IEnumerable<T> Keys {
			get { return List.Select (p => p.Key); }
		}

		public override IEnumerable<U> Values {
			get { return List.Select (p => p.Value); }
		}
	}
}
