using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http.Controllers;

namespace Breeze.ContextProvider.NH.Serialization
{
    public interface ISerializationModelRule<TModel>
    {
        ISerializationModelRule<TModel> ForProperty<TProperty>(Expression<Func<TModel, TProperty>> propExpression,
            Action<ISerializationMemberRule<TModel, TProperty>> action);

        ISerializationModelRule<TModel> CreateProperty<TProperty>(string propName, Func<TModel, TProperty> propValFunc);
    }
}
