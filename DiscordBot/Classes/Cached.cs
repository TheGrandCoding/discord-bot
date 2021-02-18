using System;

namespace DiscordBot.Classes
{
    public struct Cached<T>
    {
        public Cached(T value, int minuteExpires = 15)
        {
            _value = value;
            Set = DateTime.Now.AddMinutes(-1);
            Timeout = minuteExpires;
        }
        T _value;
        public T Value
        {
            get
            {
                return _value;
            }
            set
            {
                _value = value;
                Set = DateTime.Now;
            }
        }
        public DateTime Set { get; private set; }
        public int Timeout { get; }
        public bool Expired => Set.AddMinutes(Timeout) < DateTime.Now;
        public T GetValueOrDefault(T defaultValue = default(T))
        {
            if (Expired)
                return defaultValue;
            return Value;
        }
    }
}
