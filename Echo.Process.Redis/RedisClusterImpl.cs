﻿using System;
using System.Collections.Generic;
using System.Linq;
using StackExchange.Redis;
using Newtonsoft.Json;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using LanguageExt;
using static LanguageExt.Prelude;
using System.Threading;
using System.Reflection;
using System.Threading.Tasks;

namespace Echo
{
    class RedisClusterImpl : ICluster
    {
        const int TimeoutRetries = 5;
        readonly ClusterConfig config;
        readonly object sync = new object();
        int databaseNumber;
        ConnectionMultiplexer redis;
        readonly AtomHashMap<string, Subject<RedisValue>> subscriptions = AtomHashMap<string, Subject<RedisValue>>();

        /// <summary>
        /// Ctor
        /// </summary>
        public RedisClusterImpl(ClusterConfig config)
        {
            this.config = config;
        }

        ~RedisClusterImpl()
        {
            Dispose();
        }

        public void Dispose()
        {
            var r = redis;
            if (r != null)
            {
                if (r.IsConnected)
                {
                    r.Close();
                }

                r.Dispose();
                redis = null;
            }
        }

        public ProcessName NodeName =>
            Config.NodeName;


        /// <summary>
        /// Role that this node plays in the cluster
        /// </summary>
        public ProcessName Role =>
            Config.Role;

        /// <summary>
        /// Return true if connected to cluster
        /// </summary>
        public bool Connected =>
            redis != null;

        /// <summary>
        /// Cluster configuration
        /// </summary>
        public ClusterConfig Config =>
            config;

        /// <summary>
        /// Connect to cluster
        /// </summary>
        public void Connect()
        {
            var databaseNumber = parseUInt(Config.CatalogueName).IfNone(() => raise<uint>(new ArgumentException("Parsing CatalogueName as a number that is 0 or greater, failed.")));

            lock (sync)
            {
                if (redis == null)
                {
                    Retry(() => redis = ConnectionMultiplexer.Connect(Config.ConnectionString));
                    this.databaseNumber = (int) databaseNumber;
                }
            }
        }

        /// <summary>
        /// Disconnect from cluster
        /// </summary>
        public void Disconnect()
        {
            lock (sync)
            {
                if (redis != null)
                {
                    redis.Close(true);
                    redis.Dispose();
                    redis = null;
                }
            }
        }

        /// <summary>
        /// Publish data to a named channel
        /// </summary>
        public int PublishToChannel(string channelName, object data) =>
            Retry(() => (int) redis.GetSubscriber().Publish(
                      channelName + Config.CatalogueName,
                      JsonConvert.SerializeObject(data)
                  ));

        Subject<RedisValue> GetSubject(string channelName)
        {
            channelName += Config.CatalogueName;
            
            return subscriptions.FindOrAdd(
                channelName,
                () => {
                    var subject = new Subject<RedisValue>();
                    redis.GetSubscriber()
                         .Subscribe(channelName,
                                    (channel, value) => {
                                        if (channel == channelName && !value.IsNullOrEmpty)
                                        {
                                            subject.OnNext(value);
                                        }
                                    });
                    
                    return subject;
                });
        }

        /// <summary>
        /// Subscribe to a named channel
        /// </summary>
        public IObservable<Object> SubscribeToChannel(string channelName, Type type) =>
            GetSubject(channelName)
               .Select(value => {
                           try
                           {
                               return Some(Deserialise(value, type));
                           }
                           catch
                           {
                               return None;
                           }
                       })
               .Where(x => x.IsSome)
               .Select(x => x.IfNoneUnsafe(Ignore<Object>));

        public IObservable<T> SubscribeToChannel<T>(string channelName) =>
            GetSubject(channelName)
               .Select(value => {
                           try
                           {
                               return Some(JsonConvert.DeserializeObject<T>(value));
                           }
                           catch
                           {
                               return None;
                           }
                       })
               .Where(x => x.IsSome)
               .Select(x => x.IfNoneUnsafe(Ignore<T>));

        T Ignore<T>() =>
            default(T);

        public void UnsubscribeChannel(string channelName)
        {
            channelName += Config.CatalogueName;
            Retry(() => redis.GetSubscriber().Unsubscribe(channelName));
            if (subscriptions.Find(channelName).Case is Subject<RedisValue> ch) ch.OnCompleted();
            subscriptions.Remove(channelName);
        }

        public void SetValue(string key, object value) =>
            Retry(() => Db.StringSet(key, JsonConvert.SerializeObject(value), TimeSpan.FromDays(RedisCluster.maxDaysToPersistProcessState)));

        public T GetValue<T>(string key) =>
            Retry(() => JsonConvert.DeserializeObject<T>(Db.StringGet(key)));

        public bool Exists(string key) =>
            Retry(() => Db.KeyExists(key));

        public bool Delete(string key) =>
            Retry(() => Db.KeyDelete(key));

        public bool DeleteMany(params string[] keys) =>
            DeleteMany(keys.AsEnumerable());

        public bool DeleteMany(IEnumerable<string> keys) =>
            Retry(() => Db.KeyDelete(keys.Map(k => (RedisKey) k).ToArray()) == keys.Count());

        public int QueueLength(string key) =>
            Retry(() => (int) Db.ListLength(key));

        public int Enqueue(string key, object value) =>
            Retry(() => (int) Db.ListRightPush(key, JsonConvert.SerializeObject(value)));

        public T Peek<T>(string key)
        {
            try
            {
                var val = Retry(() => Db.ListGetByIndex(key, 0));
                return JsonConvert.DeserializeObject<T>(val);
            }
            catch
            {
                return default(T);
            }
        }

        public T Dequeue<T>(string key)
        {
            try
            {
                return Retry(() => JsonConvert.DeserializeObject<T>(Db.ListLeftPop(key)));
            }
            catch
            {
                return default(T);
            }
        }

        /// <summary>
        /// Get queue by key
        /// </summary>
        public IEnumerable<T> GetQueue<T>(string key)
        {
            if (Exists(key))
            {
                return Retry(() =>
                                 Db.ListRange(key)
                                   .Select(x => {
                                               try
                                               {
                                                   return SomeUnsafe(JsonConvert.DeserializeObject<T>(x));
                                               }
                                               catch
                                               {
                                                   return OptionUnsafe<T>.None;
                                               }
                                           })
                                   .Where(x => x.IsSome)
                                   .Select(x => x.IfNoneUnsafe(Ignore<T>))
                                   .ToList());
            }
            else
            {
                return new T[0];
            }
        }

        public bool SetExpire(string key, TimeSpan time) =>
            Retry(() => Db.KeyExpire(key, time));

        public void SetAddOrUpdate<T>(string key, T value) =>
            Retry(() => Db.SetAdd(key, JsonConvert.SerializeObject(value)));

        public Set<T> GetSet<T>(string key) =>
            Retry(() => toSet(Db.SetMembers(key).Map(x => JsonConvert.DeserializeObject<T>(x))));

        public void SetRemove<T>(string key, T value) =>
            Retry(() => Db.SetRemove(key, JsonConvert.SerializeObject(value)));

        public bool SetContains<T>(string key, T value) =>
            Retry(() => Db.SetContains(key, JsonConvert.SerializeObject(value)));

        public bool HashFieldExists(string key, string field) =>
            Retry(() => Db.HashExists(key, field));

        public void HashFieldAddOrUpdate<T>(string key, string field, T value) =>
            Retry(() => Db.HashSet(key, field, JsonConvert.SerializeObject(value)));

        public void HashFieldAddOrUpdate<T>(string key, HashMap<string, T> fields) =>
            Retry(() =>
                      Db.HashSet(
                          key,
                          fields.Map((_, v) => new HashEntry(v.Key, JsonConvert.SerializeObject(v.Value))).ToArray()
                      ));

        public bool HashFieldAddOrUpdateIfKeyExists<T>(string key, string field, T value) =>
            Retry(() => {
                      var trans = Db.CreateTransaction();
                      trans.AddCondition(Condition.KeyExists(key));
                      trans.HashSetAsync(key, field, JsonConvert.SerializeObject(value));
                      return trans.Execute();
                  });

        public bool HashFieldAddOrUpdateIfKeyExists<T>(string key, HashMap<string, T> fields) =>
            Retry(() => {
                      var trans = Db.CreateTransaction();
                      trans.AddCondition(Condition.KeyExists(key));
                      trans.HashSetAsync(
                          key,
                          fields.Map((_, v) => new HashEntry(v.Key, JsonConvert.SerializeObject(v.Value))).ToArray()
                      );

                      return trans.Execute();
                  });

        public bool DeleteHashField(string key, string field) =>
            Retry(() =>
                      Db.HashDelete(key, field));

        public int DeleteHashFields(string key, IEnumerable<string> fields) =>
            Retry(() =>
                      (int) Db.HashDelete(key, fields.Map(x => (RedisValue) x).ToArray()));

        public HashMap<string, object> GetHashFields(string key) =>
            Retry(() =>
                      Db.HashGetAll(key)
                        .Fold(
                             HashMap<string, object>(),
                             (m, e) => m.Add(e.Name, JsonConvert.DeserializeObject(e.Value)))
                        .Filter(notnull));

        public HashMap<string, T> GetHashFields<T>(string key) =>
            Retry(() =>
                      Db.HashGetAll(key)
                        .Fold(
                             HashMap<string, T>(),
                             (m, e) => m.Add(e.Name, JsonConvert.DeserializeObject<T>(e.Value)))
                        .Filter(notnull<T>));

        /// <summary>
        /// tries to deserialise redis object (hash field) to T, if fail, the object is skipped.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        public Option<T> GetHashFieldDropOnDeserialiseFailed<T>(string key, string field)
        {
            var res = Retry(() => Db.HashGet(key, field));

            if (res.IsNullOrEmpty)
            {
                return None;
            }

            try
            {
                return Optional(JsonConvert.DeserializeObject<T>(res));
            }
            catch (JsonException)
            {
                return None;
            }
        }

        public HashMap<K, T> GetHashFields<K, T>(string key, Func<string, K> keyBuilder) =>
            Retry(() =>
                      Db.HashGetAll(key)
                        .Fold(
                             HashMap<K, T>(),
                             (m, e) => m.Add(keyBuilder(e.Name), JsonConvert.DeserializeObject<T>(e.Value)))
                        .Filter(notnull<T>));

        public Option<T> GetHashField<T>(string key, string field)
        {
            var res = Retry(() => Db.HashGet(key, field));
            if (res.IsNullOrEmpty) return None;
            return JsonConvert.DeserializeObject<T>(res);
        }

        public HashMap<string, T> GetHashFields<T>(string key, IEnumerable<string> fields) =>
            Retry(() =>
                      Db.HashGet(key, fields.Map(x => (RedisValue) x).ToArray())
                        .Zip(fields)
                        .Filter(x => !x.Item1.IsNullOrEmpty)
                        .Fold(
                             HashMap<string, T>(),
                             (m, e) => m.Add(e.Item2, JsonConvert.DeserializeObject<T>(e.Item1)))
                        .Filter(notnull<T>));

        // TODO: These facts exist elsewhere - normalise
        const string userInboxSuffix = "-user-inbox";
        const string metaDataSuffix = "-metadata";
        const string regdPrefix = "/__registered/";

        /// <summary>
        /// Runs a query on all servers in the Redis cluster for the key specified
        /// with a prefix and suffix applied.  Returns a list of Redis keys
        /// </summary>
        /// <remarks>
        /// Wildcard is *
        /// </remarks>
        IEnumerable<string> QueryKeys(string keyQuery, string prefix, string suffix) =>
            Retry(() =>
                      from keys in map($"{prefix}{keyQuery}{suffix}",
                                       ibxkey =>
                                           redis.GetEndPoints()
                                                .Map(ep => redis.GetServer(ep))
                                                .Map(sv => sv.Keys(databaseNumber, ibxkey)))
                      from redisKey in keys
                      let strKey = (string) redisKey
                      select strKey);

        /// <summary>
        /// Finds all schedule keys
        /// </summary>
        /// <returns>Session keys</returns>
        public IEnumerable<string> QueryScheduleKeys(string system) =>
            QueryKeys($"/__schedule/{system}/*", "", "");

        /// <summary>
        /// Finds all session keys
        /// </summary>
        /// <returns>Session keys</returns>
        public IEnumerable<string> QuerySessionKeys() =>
            QueryKeys("sys-session-*", "", "");

        /// <summary>
        /// Finds all registered names in a role
        /// </summary>
        /// <param name="role">Role to limit search to</param>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Registered names</returns>
        public IEnumerable<ProcessName> QueryRegistered(string role, string keyQuery) =>
            map($"{regdPrefix}{role}-",
                prefix =>
                    from strKey in QueryKeys(keyQuery, prefix, "")
                    select new ProcessName(strKey.Substring(prefix.Length)));

        /// <summary>
        /// Finds all the processes based on the search pattern provided.  Note the returned
        /// ProcessIds may contain processes that aren't currently active.  You can still post
        /// to them however.
        /// </summary>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Matching ProcessIds</returns>
        public IEnumerable<ProcessId> QueryProcesses(string keyQuery) =>
            from strKey in QueryKeys(keyQuery, "", userInboxSuffix)
            select new ProcessId(strKey.Substring(0, strKey.Length - userInboxSuffix.Length));

        /// <summary>
        /// Finds all the processes based on the search pattern provided and then returns the
        /// meta-data associated with them.
        /// </summary>
        /// <param name="keyQuery">Key query.  * is a wildcard</param>
        /// <returns>Map of ProcessId to ProcessMetaData</returns>
        public HashMap<ProcessId, ProcessMetaData> QueryProcessMetaData(string keyQuery) =>
            toHashMap(
                map(QueryKeys(keyQuery, "", metaDataSuffix)
                   .Map(x => (RedisKey) x)
                   .ToArray(),
                    keys =>
                        keys.Map(x => (string) x)
                            .Map(x => (ProcessId) x.Substring(0, x.Length - metaDataSuffix.Length))
                            .Zip(Retry(() => Db.StringGet(keys)).Map(x => JsonConvert.DeserializeObject<ProcessMetaData>(x)))));

        /// <summary>
        /// retrieves all hash values for a list of keys
        /// </summary>
        /// <param name="keys"></param>
        /// <returns>map of keys and their key/value map</returns>
        public async Task<HashMap<string, HashMap<string, object>>> GetAllHashFieldsInBatch(Seq<string> keys)
        {
            var batch = Db.CreateBatch();
            var tasks = keys.Map(key => batch.HashGetAllAsync(key)
                                             .Map(h =>
                                                      (Key: key, Value: toHashMap(h.Map(r =>
                                                                                            ((string) r.Name, JsonConvert.DeserializeObject(r.Value)))))))
                            .Strict();

            batch.Execute();

            return toHashMap(await Task.WhenAll(tasks));
        }

        IDatabase Db =>
            redis.GetDatabase(databaseNumber);

        static readonly Func<Type, MethodInfo> DeserialiseFunc =
            memo<Type, MethodInfo>(type =>
                                       typeof(JsonConvert).GetMethods()
                                                          .Filter(m => m.IsGenericMethod)
                                                          .Filter(m => m.Name == "DeserializeObject")
                                                          .Filter(m => m.GetParameters().Length == 1)
                                                          .Head()
                                                          .MakeGenericMethod(type));

        public static object Deserialise(string value, Type type) =>
            DeserialiseFunc(type).Invoke(null, new[] {value});

        static T Retry<T>(Func<T> f)
        {
            static T retry(Func<T> fn)
            {
                using var ev = new AutoResetEvent(false);
                for (var i = 0; ; i++)
                {
                    try
                    {
                        return fn();
                    }
                    catch (Exception ex) when (ex is TimeoutException || ex is RedisConnectionException || ex is RedisServerException)
                    {
                        if (i == TimeoutRetries)
                        {
                            throw;
                        }

                        // Backing off wait time
                        // 0 - immediately
                        // 1 - 100 ms
                        // 2 - 400 ms
                        // 3 - 900 ms
                        // 4 - 1600 ms
                        // Maximum wait == 3000ms
                        ev.WaitOne(i * i * 100);
                    }
                }
            }

            try
            {
                return f();
            }
            catch (Exception ex) when (ex is TimeoutException || ex is RedisConnectionException || ex is RedisServerException)
            {
                return retry(f);
            }
        }

        static void Retry(Action f) =>
            Retry(() => { f(); return unit; });
    }
}