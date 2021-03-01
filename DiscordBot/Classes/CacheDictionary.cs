using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace DiscordBot.Classes
{
    public class CacheDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private ConcurrentDictionary<TKey, Cached<TValue>> _dict;
        private int expireMinutes;
        public CacheDictionary(int expires = 15)
        {
            _dict = new ConcurrentDictionary<TKey, Cached<TValue>>();
            expireMinutes = expires;
        }

        public TValue this[TKey key]
        {
            get
            {
                if (_dict.TryGetValue(key, out var val) && !val.Expired)
                    return val.Value;
                throw new KeyNotFoundException();
            }
            set
            {
                if (_dict.TryGetValue(key, out var val))
                    val.Value = value;
                else
                    _dict[key] = new Cached<TValue>(value, expireMinutes);
            }
        }

        public ICollection<TKey> Keys => _dict.Keys;

        public ICollection<TValue> Values {  get
            {
                var ls = new List<TValue>();
                foreach(var value in _dict.Values)
                {
                    if (!value.Expired)
                        ls.Add(value.Value);
                }
                return ls.ToArray();
            } }

        public int Count => _dict.Count;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            this[key] = value;
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            this[item.Key] = item.Value;
        }

        public void Clear()
        {
            _dict.Clear();
        }

        public bool Contains(KeyValuePair<TKey, TValue> item)
        {
            if (TryGetValue(item.Key, out var val))
                return val == null ? item.Value == null : val.Equals(item);
            return false;
        }

        public bool ContainsKey(TKey key)
        {
            return _dict.ContainsKey(key);
        }

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            throw new NotImplementedException();
        }

        public bool Remove(TKey key)
        {
            return _dict.Remove(key, out _);
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            return Remove(item.Key);
        }

        public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
        {
            if(_dict.TryGetValue(key, out var cache) && cache.Expired == false)
            {
                value = cache.Value;
                return true;
            }
            value = default(TValue);
            return false;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw new NotImplementedException();
        }
    }
}
