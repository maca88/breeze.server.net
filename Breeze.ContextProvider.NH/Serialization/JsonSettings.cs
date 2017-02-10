using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Web.Http.Controllers;
using Breeze.ContextProvider.NH.Extensions;

namespace Breeze.ContextProvider.NH.Serialization
{
    public class JsonSettings
    {
        protected static Dictionary<Type, IList<SerializationModelRule>> serializationModelRules = new Dictionary<Type, IList<SerializationModelRule>>();

        static JsonSettings()
        {
        }

        public static IList<SerializationModelRule> GetSerializationModelRules<TModel>()
        {
            return GetSerializationModelRules(typeof (TModel));
        }

        public static IList<SerializationModelRule> GetSerializationModelRules(Type modelType)
        {
            return serializationModelRules.ContainsKey(modelType)
                ? serializationModelRules[modelType]
                : null;
        }

        public static ISerializationModelRule<TModel> SerializationRule<TModel>()
        {
            var type = typeof(TModel);
            var modelRule = new SerializationModelRule<TModel>();
            if (!serializationModelRules.ContainsKey(type))
                serializationModelRules.Add(type, new List<SerializationModelRule>());
            serializationModelRules[type].Add(modelRule);
            return modelRule;
        }
    }
}
