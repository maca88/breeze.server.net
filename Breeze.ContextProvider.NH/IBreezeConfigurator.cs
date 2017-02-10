using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.ContextProvider.NH
{
    public interface IBreezeConfigurator
    {
        IModelConfiguration GetModelConfiguration(Type modelType);

        IModelConfiguration GetModelConfiguration<TModel>() where TModel : class;

        IModelConfiguration<TModel> Configure<TModel>();

        IModelConfiguration Configure(Type type);
    }
}
