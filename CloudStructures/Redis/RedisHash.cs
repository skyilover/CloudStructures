﻿using BookSleeve;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

// RedisDictionary/Hash/Class
namespace CloudStructures.Redis
{
    internal class HashScript
    {
        public const string IncrementLimitByMax = @"
local inc = tonumber(ARGV[1])
local max = tonumber(ARGV[2])
local x = redis.call('hincrby', KEYS[1], KEYS[2], inc)
if(x > max) then
    redis.call('hset', KEYS[1], KEYS[2], max)
    x = max
end
return x";

        public const string IncrementLimitByMin = @"
local inc = tonumber(ARGV[1])
local min = tonumber(ARGV[2])
local x = redis.call('hincrby', KEYS[1], KEYS[2], inc)
if(x < min) then
    redis.call('hset', KEYS[1], KEYS[2], min)
    x = min
end
return x";

        public const string IncrementFloatLimitByMax = @"
local inc = tonumber(ARGV[1])
local max = tonumber(ARGV[2])
local x = tonumber(redis.call('hincrbyfloat', KEYS[1], KEYS[2], inc))
if(x > max) then
    redis.call('hset', KEYS[1], KEYS[2], max)
    x = max
end
return tostring(x)";

        public const string IncrementFloatLimitByMin = @"
local inc = tonumber(ARGV[1])
local min = tonumber(ARGV[2])
local x = tonumber(redis.call('hincrbyfloat', KEYS[1], KEYS[2], inc))
if(x < min) then
    redis.call('hset', KEYS[1], KEYS[2], min)
    x = min
end
return tostring(x)";
    }

    public class RedisDictionary<T>
    {
        const string CallType = "RedisDictionary";

        public string Key { get; private set; }
        public RedisSettings Settings { get; private set; }


        public RedisDictionary(RedisSettings settings, string hashKey)
        {
            this.Settings = settings;
            this.Key = hashKey;
        }

        public RedisDictionary(RedisGroup connectionGroup, string hashKey)
            : this(connectionGroup.GetSettings(hashKey), hashKey)
        {
        }

        protected RedisConnection Connection
        {
            get
            {
                return Settings.GetConnection();
            }
        }

        protected IHashCommands Command
        {
            get
            {
                return Connection.Hashes;
            }
        }

        /// <summary>
        /// HEXISTS http://redis.io/commands/hexists
        /// </summary>
        public Task<bool> Exists(string field, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Exists(Settings.Db, Key, field, queueJump).ConfigureAwait(false);
                return Pair.Create(new { field }, r);
            });
        }

        /// <summary>
        /// HGET http://redis.io/commands/hget
        /// </summary>
        public Task<T> Get(string field, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = await Command.Get(Settings.Db, Key, field, queueJump).ConfigureAwait(false);
                var r = Settings.ValueConverter.Deserialize<T>(v);
                return Pair.Create(new { field }, r);
            });
        }

        /// <summary>
        /// HMGET http://redis.io/commands/hmget
        /// </summary>
        public Task<T[]> Get(string[] fields, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = await Command.Get(Settings.Db, Key, fields, queueJump).ConfigureAwait(false);
                var r = v.Select(Settings.ValueConverter.Deserialize<T>).ToArray();
                return Pair.Create(new { fields }, r);
            });
        }

        /// <summary>
        /// HGETALL http://redis.io/commands/hgetall
        /// </summary>
        public Task<Dictionary<string, T>> GetAll(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, async () =>
            {
                var v = await Command.GetAll(Settings.Db, Key, queueJump).ConfigureAwait(false);
                var r = v.ToDictionary(x => x.Key, x => Settings.ValueConverter.Deserialize<T>(x.Value));
                return r;
            });
        }

        /// <summary>
        /// HKEYS http://redis.io/commands/hkeys
        /// </summary>
        public Task<string[]> GetKeys(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Command.GetKeys(Settings.Db, Key, queueJump);
            });
        }

        /// <summary>
        /// HLEN http://redis.io/commands/hlen
        /// </summary>
        public Task<long> GetLength(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Command.GetLength(Settings.Db, Key, queueJump);
            });
        }

        /// <summary>
        /// HVALS http://redis.io/commands/hvals
        /// </summary>
        public Task<T[]> GetValues(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, async () =>
            {
                var v = await Command.GetValues(Settings.Db, Key, queueJump).ConfigureAwait(false);
                return v.Select(Settings.ValueConverter.Deserialize<T>).ToArray();
            });
        }

        /// <summary>
        /// HINCRBY http://redis.io/commands/hincrby
        /// </summary>
        public Task<long> Increment(string field, int value = 1, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Increment(Settings.Db, Key, field, value, queueJump).ConfigureAwait(false);
                return Pair.Create(new { field, value }, r);
            });
        }

        public Task<long> IncrementLimitByMax(string field, int value, int max, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementLimitByMax, new[] { Key, field }, new object[] { value, max }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = (long)(await v.ConfigureAwait(false));
                return Pair.Create(new { field, value, max }, r);
            });
        }

        /// <summary>
        /// HINCRBYFLOAT http://redis.io/commands/hincrbyfloat
        /// </summary>
        public Task<double> Increment(string field, double value, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Increment(Settings.Db, Key, field, value, queueJump).ConfigureAwait(false);
                return Pair.Create(new { field, value }, r);
            });
        }

        public Task<double> IncrementLimitByMax(string field, double value, double max, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementFloatLimitByMax, new[] { Key, field }, new object[] { value, max }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = double.Parse((string)(await v.ConfigureAwait(false)));
                return Pair.Create(new { field, value, max }, r);
            });
        }

        public Task<long> IncrementLimitByMin(string field, int value, int min, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementLimitByMin, new[] { Key, field }, new object[] { value, min }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = (long)(await v.ConfigureAwait(false));
                return Pair.Create(new { field, value, min }, r);
            });
        }

        public Task<double> IncrementLimitByMin(string field, double value, double min, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementFloatLimitByMin, new[] { Key, field }, new object[] { value, min }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = double.Parse((string)(await v.ConfigureAwait(false)));
                return Pair.Create(new { field, value, min }, r);
            });
        }

        /// <summary>
        /// HDEL http://redis.io/commands/hdel
        /// </summary>
        public Task<bool> Remove(string field, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Remove(Settings.Db, Key, field, queueJump).ConfigureAwait(false);
                return Pair.Create(new { field }, r);
            });
        }

        /// <summary>
        /// HDEL http://redis.io/commands/hdel
        /// </summary>
        public Task<long> Remove(string[] fields, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Remove(Settings.Db, Key, fields, queueJump).ConfigureAwait(false);
                return Pair.Create(new { fields }, r);
            });
        }

        /// <summary>
        /// HMSET http://redis.io/commands/hmset
        /// </summary>
        public Task Set(Dictionary<string, T> values, bool queueJump = false)
        {
            return TraceHelper.RecordSend(Settings, Key, CallType, async () =>
            {
                var v = values.ToDictionary(x => x.Key, x => Settings.ValueConverter.Serialize(x.Value));
                await Command.Set(Settings.Db, Key, v, queueJump).ConfigureAwait(false);
                return new { values };
            });
        }

        /// <summary>
        /// HSET http://redis.io/commands/hset
        /// </summary>
        public Task<bool> Set(string field, T value, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Set(Settings.Db, Key, field, Settings.ValueConverter.Serialize(value), queueJump).ConfigureAwait(false);
                return Pair.Create(new { field, value }, r);
            });
        }

        /// <summary>
        /// HSETNX http://redis.io/commands/hsetnx
        /// </summary>
        public Task<bool> SetIfNotExists(string field, T value, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.SetIfNotExists(Settings.Db, Key, field, Settings.ValueConverter.Serialize(value), queueJump).ConfigureAwait(false);
                return Pair.Create(new { field, value }, r);
            });
        }

        /// <summary>
        /// expire subtract Datetime.Now
        /// </summary>
        public Task<bool> SetExpire(DateTime expire, bool queueJump = false)
        {
            return SetExpire(expire - DateTime.Now, queueJump);
        }

        public Task<bool> SetExpire(TimeSpan expire, bool queueJump = false)
        {
            return SetExpire((int)expire.TotalSeconds, queueJump);
        }

        public Task<bool> SetExpire(int seconds, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Connection.Keys.Expire(Settings.Db, Key, seconds, queueJump).ConfigureAwait(false);
                return Pair.Create(new { seconds }, r);
            });
        }

        public Task<bool> KeyExists(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Connection.Keys.Exists(Settings.Db, Key, queueJump);
            });
        }

        public Task<bool> Clear(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Connection.Keys.Remove(Settings.Db, Key, queueJump);
            });
        }
    }

    public class RedisHash
    {
        const string CallType = "RedisHash";

        public string Key { get; private set; }
        public RedisSettings Settings { get; private set; }

        public RedisHash(RedisSettings settings, string hashKey)
        {
            this.Settings = settings;
            this.Key = hashKey;
        }

        public RedisHash(RedisGroup connectionGroup, string hashKey)
            : this(connectionGroup.GetSettings(hashKey), hashKey)
        {
        }

        protected RedisConnection Connection
        {
            get
            {
                return Settings.GetConnection();
            }
        }

        protected IHashCommands Command
        {
            get
            {
                return Connection.Hashes;
            }
        }

        /// <summary>
        /// HEXISTS http://redis.io/commands/hexists
        /// </summary>
        public Task<bool> Exists(string field, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Exists(Settings.Db, Key, field, queueJump).ConfigureAwait(false);
                return Pair.Create(new { field }, r);
            });
        }

        /// <summary>
        /// HGET http://redis.io/commands/hget
        /// </summary>
        public Task<T> Get<T>(string field, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = await Command.Get(Settings.Db, Key, field, queueJump).ConfigureAwait(false);
                var r = Settings.ValueConverter.Deserialize<T>(v);
                return Pair.Create(new { field }, r);
            });
        }

        /// <summary>
        /// HMGET http://redis.io/commands/hmget
        /// </summary>
        public Task<T[]> Get<T>(string[] fields, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = await Command.Get(Settings.Db, Key, fields, queueJump).ConfigureAwait(false);
                var r = v.Select(Settings.ValueConverter.Deserialize<T>).ToArray();
                return Pair.Create(new { fields }, r);
            });
        }

        /// <summary>
        /// HGETALL http://redis.io/commands/hgetall
        /// </summary>
        public Task<Dictionary<string, T>> GetAll<T>(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, async () =>
            {
                var v = await Command.GetAll(Settings.Db, Key, queueJump).ConfigureAwait(false);
                var r = v.ToDictionary(x => x.Key, x => Settings.ValueConverter.Deserialize<T>(x.Value));
                return r;
            });
        }

        /// <summary>
        /// HKEYS http://redis.io/commands/hkeys
        /// </summary>
        public Task<string[]> GetKeys(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Command.GetKeys(Settings.Db, Key, queueJump);
            });
        }

        /// <summary>
        /// HLEN http://redis.io/commands/hlen
        /// </summary>
        public Task<long> GetLength(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Command.GetLength(Settings.Db, Key, queueJump);
            });
        }

        /// <summary>
        /// HVALS http://redis.io/commands/hvals
        /// </summary>
        public Task<T[]> GetValues<T>(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, async () =>
            {
                var v = await Command.GetValues(Settings.Db, Key, queueJump).ConfigureAwait(false);
                return v.Select(Settings.ValueConverter.Deserialize<T>).ToArray();
            });
        }

        /// <summary>
        /// HINCRBY http://redis.io/commands/hincrby
        /// </summary>
        public Task<long> Increment(string field, int value = 1, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Increment(Settings.Db, Key, field, value, queueJump).ConfigureAwait(false);
                return Pair.Create(new { field, value }, r);
            });
        }

        /// <summary>
        /// HINCRBYFLOAT http://redis.io/commands/hincrbyfloat
        /// </summary>
        public Task<double> Increment(string field, double value, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Increment(Settings.Db, Key, field, value, queueJump).ConfigureAwait(false);
                return Pair.Create(new { field, value }, r);
            });
        }

        public Task<long> IncrementLimitByMax(string field, int value, int max, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementLimitByMax, new[] { Key, field }, new object[] { value, max }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = (long)(await v.ConfigureAwait(false));
                return Pair.Create(new { field, value, max }, r);
            });
        }

        public Task<double> IncrementLimitByMax(string field, double value, double max, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementFloatLimitByMax, new[] { Key, field }, new object[] { value, max }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = double.Parse((string)(await v.ConfigureAwait(false)));
                return Pair.Create(new { field, value, max }, r);
            });
        }

        public Task<long> IncrementLimitByMin(string field, int value, int min, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementLimitByMin, new[] { Key, field }, new object[] { value, min }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = (long)(await v.ConfigureAwait(false));
                return Pair.Create(new { field, value, min }, r);
            });
        }

        public Task<double> IncrementLimitByMin(string field, double value, double min, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementFloatLimitByMin, new[] { Key, field }, new object[] { value, min }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = double.Parse((string)(await v.ConfigureAwait(false)));
                return Pair.Create(new { field, value, min }, r);
            });
        }

        /// <summary>
        /// HDEL http://redis.io/commands/hdel
        /// </summary>
        public Task<bool> Remove(string field, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Remove(Settings.Db, Key, field, queueJump);
                return Pair.Create(new { field }, r);
            });
        }
        /// <summary>
        /// HDEL http://redis.io/commands/hdel
        /// </summary>
        public Task<long> Remove(string[] fields, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Remove(Settings.Db, Key, fields, queueJump);
                return Pair.Create(new { fields }, r);
            });
        }

        /// <summary>
        /// HMSET http://redis.io/commands/hmset
        /// </summary>
        public Task Set(Dictionary<string, object> values, bool queueJump = false)
        {
            return TraceHelper.RecordSend(Settings, Key, CallType, async () =>
            {
                var v = values.ToDictionary(x => x.Key, x => Settings.ValueConverter.Serialize(x.Value));
                await Command.Set(Settings.Db, Key, v, queueJump);
                return new { values };
            });
        }

        /// <summary>
        /// HSET http://redis.io/commands/hset
        /// </summary>
        public Task<bool> Set(string field, object value, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Set(Settings.Db, Key, field, Settings.ValueConverter.Serialize(value), queueJump);
                return Pair.Create(new { field, value }, r);
            });
        }

        /// <summary>
        /// HSETNX http://redis.io/commands/hsetnx
        /// </summary>
        public Task<bool> SetIfNotExists(string field, object value, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.SetIfNotExists(Settings.Db, Key, field, Settings.ValueConverter.Serialize(value), queueJump);
                return Pair.Create(new { field, value }, r);
            });
        }

        /// <summary>
        /// expire subtract Datetime.Now
        /// </summary>
        public Task<bool> SetExpire(DateTime expire, bool queueJump = false)
        {
            return SetExpire(expire - DateTime.Now, queueJump);
        }

        public Task<bool> SetExpire(TimeSpan expire, bool queueJump = false)
        {
            return SetExpire((int)expire.TotalSeconds, queueJump);
        }

        public Task<bool> SetExpire(int seconds, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Connection.Keys.Expire(Settings.Db, Key, seconds, queueJump).ConfigureAwait(false);
                return Pair.Create(new { seconds }, r);
            });
        }

        public Task<bool> KeyExists(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Connection.Keys.Exists(Settings.Db, Key, queueJump);
            });
        }

        public Task<bool> Clear(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Connection.Keys.Remove(Settings.Db, Key, queueJump);
            });
        }
    }


    /// <summary>
    /// Class mapped RedisHash
    /// </summary>
    public class RedisClass<T> where T : class, new()
    {
        const string CallType = "RedisClass";

        public string Key { get; private set; }
        public RedisSettings Settings { get; private set; }

        public RedisClass(RedisSettings settings, string hashKey)
        {
            this.Settings = settings;
            this.Key = hashKey;
        }

        public RedisClass(RedisGroup connectionGroup, string hashKey)
            : this(connectionGroup.GetSettings(hashKey), hashKey)
        {
        }

        protected RedisConnection Connection
        {
            get
            {
                return Settings.GetConnection();
            }
        }

        protected IHashCommands Command
        {
            get
            {
                return Connection.Hashes;
            }
        }

        public Task<T> GetValue(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, async () =>
            {
                var data = await Command.GetAll(Settings.Db, Key, queueJump).ConfigureAwait(false);
                if (data == null || data.Count == 0)
                {
                    return null;
                }

                var accessor = FastMember.TypeAccessor.Create(typeof(T), allowNonPublicAccessors: false);
                var result = (T)accessor.CreateNew();

                foreach (var member in accessor.GetMembers())
                {
                    byte[] value;
                    if (data.TryGetValue(member.Name, out value))
                    {
                        accessor[result, member.Name] = Settings.ValueConverter.Deserialize(member.Type, value);
                    }
                }

                return result;
            });
        }

        /// <summary>
        /// expire subtract Datetime.Now
        /// </summary>
        public Task<T> GetValueOrSet(Func<T> valueFactory, DateTime expire, bool configureAwait = true, bool queueJump = false)
        {
            return GetValueOrSet(valueFactory, expire - DateTime.Now, configureAwait, queueJump);
        }

        public Task<T> GetValueOrSet(Func<T> valueFactory, TimeSpan expire, bool configureAwait = true, bool queueJump = false)
        {
            return GetValueOrSet(valueFactory, (int)expire.TotalSeconds, configureAwait, queueJump);
        }

        public async Task<T> GetValueOrSet(Func<T> valueFactory, int? expirySeconds = null, bool configureAwait = true, bool queueJump = false)
        {
            var value = await GetValue(queueJump).ConfigureAwait(configureAwait); // keep valueFactory synchronization context
            if (value == null)
            {
                value = valueFactory();
                if (expirySeconds != null)
                {
                    var a = SetValue(value);
                    var b = SetExpire(expirySeconds.Value, queueJump);
                    await Task.WhenAll(a, b).ConfigureAwait(false);
                }
                else
                {
                    await SetValue(value).ConfigureAwait(false);
                }
            }

            return value;
        }

        /// <summary>
        /// expire subtract Datetime.Now
        /// </summary>
        public Task<T> GetValueOrSet(Func<Task<T>> valueFactory, DateTime expire, bool configureAwait = true, bool queueJump = false)
        {
            return GetValueOrSet(valueFactory, expire - DateTime.Now, configureAwait, queueJump);
        }

        public Task<T> GetValueOrSet(Func<Task<T>> valueFactory, TimeSpan expire, bool configureAwait = true, bool queueJump = false)
        {
            return GetValueOrSet(valueFactory, (int)expire.TotalSeconds, configureAwait, queueJump);
        }

        public async Task<T> GetValueOrSet(Func<Task<T>> valueFactory, int? expirySeconds = null, bool configureAwait = true, bool queueJump = false)
        {
            var value = await GetValue(queueJump).ConfigureAwait(configureAwait); // keep valueFactory synchronization context
            if (value == null)
            {
                value = await valueFactory().ConfigureAwait(configureAwait);
                if (expirySeconds != null)
                {
                    var a = SetValue(value);
                    var b = SetExpire(expirySeconds.Value, queueJump);
                    await Task.WhenAll(a, b).ConfigureAwait(false);
                }
                else
                {
                    await SetValue(value).ConfigureAwait(false);
                }
            }

            return value;
        }

        public Task SetValue(T value, bool queueJump = false)
        {
            return TraceHelper.RecordSend(Settings, Key, CallType, async () =>
            {
                var accessor = FastMember.TypeAccessor.Create(typeof(T), allowNonPublicAccessors: false);
                var members = accessor.GetMembers();
                var values = new Dictionary<string, byte[]>(members.Count);
                foreach (var member in members)
                {
                    values.Add(member.Name, Settings.ValueConverter.Serialize(accessor[value, member.Name]));
                }

                await Command.Set(Settings.Db, Key, values, queueJump).ConfigureAwait(false);

                return new { value };
            });
        }

        public Task<bool> SetField(string field, object value, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Set(Settings.Db, Key, field, Settings.ValueConverter.Serialize(value), queueJump).ConfigureAwait(false);
                return Pair.Create(new { field, value }, r);
            });
        }

        public Task SetFields(IEnumerable<KeyValuePair<string, object>> fields, bool queueJump = false)
        {
            return TraceHelper.RecordSend(Settings, Key, CallType, async () =>
            {
                var values = fields.ToDictionary(x => x.Key, x => Settings.ValueConverter.Serialize(x.Value));
                await Command.Set(Settings.Db, Key, values, queueJump).ConfigureAwait(false);

                return new { fields };
            });
        }

        public Task<TField> GetField<TField>(string field, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = await Command.Get(Settings.Db, Key, field, queueJump).ConfigureAwait(false);
                var r = Settings.ValueConverter.Deserialize<TField>(v);
                return Pair.Create(new { field }, r);
            });
        }

        public Task<long> Increment(string field, int value = 1, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Increment(Settings.Db, Key, field, value, queueJump).ConfigureAwait(false);
                return Pair.Create(new { field, value }, r);
            });
        }

        public Task<double> Increment(string field, double value, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Command.Increment(Settings.Db, Key, field, value, queueJump).ConfigureAwait(false);
                return Pair.Create(new { field, value }, r);
            });
        }

        public Task<long> IncrementLimitByMax(string field, int value, int max, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementLimitByMax, new[] { Key, field }, new object[] { value, max }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = (long)(await v.ConfigureAwait(false));
                return Pair.Create(new { field, value, max }, r);
            });
        }

        public Task<double> IncrementLimitByMax(string field, double value, double max, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementFloatLimitByMax, new[] { Key, field }, new object[] { value, max }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = double.Parse((string)(await v.ConfigureAwait(false)));
                return Pair.Create(new { field, value, max }, r);
            });
        }

        public Task<long> IncrementLimitByMin(string field, int value, int min, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementLimitByMin, new[] { Key, field }, new object[] { value, min }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = (long)(await v.ConfigureAwait(false));

                return Pair.Create(new { field, value, min }, r);
            });
        }

        public Task<double> IncrementLimitByMin(string field, double value, double min, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var v = Connection.Scripting.Eval(Settings.Db, HashScript.IncrementFloatLimitByMin, new[] { Key, field }, new object[] { value, min }, useCache: true, inferStrings: true, queueJump: queueJump);
                var r = double.Parse((string)(await v.ConfigureAwait(false)));
                return Pair.Create(new { field, value, min }, r);
            });
        }

        /// <summary>
        /// expire subtract Datetime.Now
        /// </summary>
        public Task<bool> SetExpire(DateTime expire, bool queueJump = false)
        {
            return SetExpire(expire - DateTime.Now, queueJump);
        }

        public Task<bool> SetExpire(TimeSpan expire, bool queueJump = false)
        {
            return SetExpire((int)expire.TotalSeconds, queueJump);
        }

        public Task<bool> SetExpire(int seconds, bool queueJump = false)
        {
            return TraceHelper.RecordSendAndReceive(Settings, Key, CallType, async () =>
            {
                var r = await Connection.Keys.Expire(Settings.Db, Key, seconds, queueJump).ConfigureAwait(false);
                return Pair.Create(new { seconds }, r);
            });
        }

        public Task<bool> KeyExists(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Connection.Keys.Exists(Settings.Db, Key, queueJump);
            });
        }

        public Task<bool> Clear(bool queueJump = false)
        {
            return TraceHelper.RecordReceive(Settings, Key, CallType, () =>
            {
                return Connection.Keys.Remove(Settings.Db, Key, queueJump);
            });
        }
    }
}