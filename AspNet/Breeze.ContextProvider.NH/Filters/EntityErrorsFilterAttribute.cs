﻿using System.Collections.Generic;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace Breeze.ContextProvider.NH.Filters
{
    public class EntityErrorsFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(HttpActionExecutedContext context)
        {
            if (context.Exception is EntityErrorsException)
            {
                var e = (EntityErrorsException)context.Exception;
                var error = new SaveError(e.Message, e.EntityErrors);
                var resp = new HttpResponseMessage(e.StatusCode)
                {
                    Content = new ObjectContent(typeof(SaveError), error, JsonFormatter.Create()),
                };
                context.Response = resp;
            }
        }
    }

    internal class EntityErrorsFilterProvider : IFilterProvider
    {
        public EntityErrorsFilterProvider(EntityErrorsFilterAttribute filter)
        {
            _filter = filter;
        }

        public IEnumerable<FilterInfo> GetFilters(HttpConfiguration configuration, HttpActionDescriptor actionDescriptor)
        {
            return new[] { new FilterInfo(_filter, FilterScope.Controller) };
        }

        private readonly EntityErrorsFilterAttribute _filter;
    }
}
