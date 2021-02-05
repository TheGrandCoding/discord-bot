using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;

namespace qBitApi.REST
{
    public class DictBuilder : DictBuilder<string, object> { }
    public class DictBuilder<TKey, TValue>
    {
        public Dictionary<TKey, TValue> _dict = new Dictionary<TKey, TValue>();

        public void Add(TKey k, TValue v)
        {
            _dict.Add(k, v);
        }
        public DictBuilder<TKey, TValue> With(TKey k, TValue v)
        {
            Add(k, v);
            return this;
        }
        public TValue this[TKey key]
        {
            get => _dict[key];
            set => _dict[key] = value;
        }
        public IReadOnlyDictionary<TKey, TValue> Build() => (ImmutableDictionary<TKey, TValue>)this;

        public static implicit operator Dictionary<TKey, TValue>(DictBuilder<TKey, TValue> dict)
        {
            return dict._dict;
        }
        public static implicit operator ImmutableDictionary<TKey, TValue>(DictBuilder<TKey, TValue> dict) 
            => dict._dict.ToImmutableDictionary();
    }
}
