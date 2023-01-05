using DBreeze;
using DBreeze.AspNet;
using DBreeze.Utils;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;

// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection
{
    public static class ServiceCollectionDBreezeExtensions
    {
        private static void SetNewtonsoftJsonSerializer()
        {
            CustomSerializator.ByteArraySerializator = o =>
            {
                using MemoryStream stream = new();
                using BsonDataWriter writer = new(stream);
                JsonSerializer serializer = new();
                serializer.Serialize(writer, o);
                return stream.ToArray();
            };
            CustomSerializator.ByteArrayDeSerializator = (bt, t) =>
            {
                using MemoryStream stream = new(bt);
                using BsonDataReader reader = new(stream);
                JsonSerializer serializer = new();
                return serializer.Deserialize(reader, t);
            };
        }
        // ReSharper disable once MemberCanBePrivate.Global
        public static IServiceCollection AddDBreeze(this IServiceCollection services, Action<DBreezeConfiguration> configure)
        {
            SetNewtonsoftJsonSerializer();
            services.Configure(configure);
            services.AddSingleton<IDBreezeEngineProxyChildFactory, DBreezeEngineProxy>();
            services.AddTransient<IDBreezeEngineProxy>(provider =>
                provider.GetRequiredService<IDBreezeEngineProxyChildFactory>().CreateDisposableChild());
            return services;
        }
        // ReSharper disable once MemberCanBePrivate.Global
        public static IServiceCollection AddDBreeze(this IServiceCollection services)
        {
            return services.AddDBreeze(opt =>
            {
                opt.DBreezeDataFolderName = "./db";
            });
        }
    }
}