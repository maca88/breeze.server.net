using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using Breeze.ContextProvider.NH.Extensions;

namespace Breeze.ContextProvider.NH.Serialization
{
    public class SerializationModelRule<TModel> : SerializationModelRule, ISerializationModelRule<TModel>
    {
        public SerializationModelRule()
        {
            ModelType = typeof(TModel);
        }

        public ISerializationModelRule<TModel> ForProperty<TProperty>(Expression<Func<TModel, TProperty>> propExpression, Action<ISerializationMemberRule<TModel, TProperty>> action)
        {
            var propName = propExpression.GetFullPropertyName();
            var propInfo = ModelType.GetProperty(propName);
            if (propInfo == null)
                throw new NullReferenceException(string.Format("Type '{0}' does not contain a property with name '{1}'.", ModelType, propName));
            var memberRule = new SerializationMemberRule<TModel, TProperty>(propInfo);
            if (action != null)
                action(memberRule);
            MemberRules[propName] = memberRule;
            return this;
        }

        public ISerializationModelRule<TModel> CreateProperty<TProperty>(string propName, Func<TModel, TProperty> propValFunc)
        {
            if (propValFunc != null)
                AdditionalProperties[propName] = o => propValFunc((TModel)o);
            return this;
        }
    }

    public class SerializationModelRule
    {
        public SerializationModelRule()
        {
            MemberRules = new Dictionary<string, SerializationMemberRule>();
            AdditionalProperties = new Dictionary<string, Func<object, object>>();
        }

        public Type ModelType { get; set; }

        public Dictionary<string, SerializationMemberRule> MemberRules { get; set; }

        public Dictionary<string, Func<object, object>> AdditionalProperties { get; set; }

    }
}
