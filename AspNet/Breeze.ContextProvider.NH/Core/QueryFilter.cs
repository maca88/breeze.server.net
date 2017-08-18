using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Reflection;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Breeze.Core;
using Breeze.WebApi2;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NHibernate;
using NHibernate.Linq;
using QueryResult = Breeze.Core.QueryResult;

namespace Breeze.ContextProvider.NH.Core
{
    public class BreezeQueryFilterAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext context)
        {
            if (!context.ModelState.IsValid)
            {
                context.Response = context.Request.CreateResponse(HttpStatusCode.BadRequest, context.ModelState, GetJsonFormatter(context.Request));
            }
        }

        public override void OnActionExecuted(HttpActionExecutedContext context)
        {
            var qs = QueryFns.ExtractAndDecodeQueryString(context);
            if (qs == null)
            {
                base.OnActionExecuted(context);
                return;
            }

            var queryable = QueryFns.ExtractQueryable(context);
            if (queryable == null)
            {
                base.OnActionExecuted(context);
            }

            var eq = new EntityQuery(qs);
            var eleType = Breeze.Core.TypeFns.GetElementType(queryable.GetType());
            eq.Validate(eleType);


            int? inlineCount = null;

            var originalQueryable = queryable;
            queryable = eq.ApplyWhere(queryable, eleType);

            if (eq.IsInlineCountEnabled)
            {
                inlineCount = (int)Queryable.Count((dynamic)queryable);
            }

            queryable = eq.ApplyOrderBy(queryable, eleType);
            queryable = eq.ApplySkip(queryable, eleType);
            queryable = eq.ApplyTake(queryable, eleType);
            queryable = eq.ApplySelect(queryable, eleType);
            queryable = eq.ApplyExpand(queryable, eleType);


            if (queryable != originalQueryable)
            {
                // if a select or expand was encountered we need to
                // execute the DbQueries here, so that any exceptions thrown can be properly returned.
                // if we wait to have the query executed within the serializer, some exceptions will not
                // serialize properly.
                var listResult = Enumerable.ToList((dynamic)queryable);
                var qr = new QueryResult(listResult, inlineCount);
                
                var session = GetSession(queryable);
                if (session != null)
                {
                    Close(session);
                }
                
                context.Response = context.Request.CreateResponse(HttpStatusCode.OK, qr, GetJsonFormatter(context.Request));
            }
            
            base.OnActionExecuted(context);
        }

        /// <summary>
        /// Return the Breeze-specific <see cref="MediaTypeFormatter"/> that formats
        /// content to JSON. This formatter must be tailored to work with Breeze clients. 
        /// </summary>
        /// <remarks>
        /// By default returns the Breeze <see cref="WebApi2.JsonFormatter"/>.
        /// Override it to substitute a custom JSON formatter.
        /// </remarks>
        protected virtual JsonMediaTypeFormatter GetJsonFormatter(HttpRequestMessage request)
        {
            var formatter = JsonFormatter.Create();
            var jsonSerializer = formatter.SerializerSettings;
            if (!formatter.SerializerSettings.Converters.Any(o => o is NHibernateProxyJsonConverter))
                jsonSerializer.Converters.Add(new NHibernateProxyJsonConverter());
            jsonSerializer.ContractResolver = (IContractResolver)request.GetDependencyScope().GetService(typeof(NHibernateContractResolver));
            /* Error handling is not needed anymore. NHibernateContractResolver will take care of non initialized properties*/
            //FIX: Still errors occurs
            jsonSerializer.Error = (sender, args) =>
            {
                // When the NHibernate session is closed, NH proxies throw LazyInitializationException when
                // the serializer tries to access them.  We want to ignore those exceptions.
                var error = args.ErrorContext.Error;
                if (error is LazyInitializationException || error is ObjectDisposedException)
                    args.ErrorContext.Handled = true;
            };
            return formatter;
        }

        private static void Close(ISession session)
        {
            if (session == null || !session.IsOpen) return;
            if (session.GetSessionImplementation().TransactionInProgress)
            {
                var tx = session.Transaction;
                try
                {
                    if (tx.IsActive) tx.Commit();
                    session.Close();
                }
                catch (Exception)
                {
                    if (tx.IsActive) tx.Rollback();
                    throw;
                }
            }
            else
            {
                session.Close();
            }
        }

        /// <summary>
        /// Get the ISession from the IQueryable.
        /// </summary>
        /// <param name="queryable"></param>
        /// <returns>the session if queryable.Provider is NHibernate.Linq.DefaultQueryProvider, else null</returns>
        private static ISession GetSession(IQueryable queryable)
        {
            if (queryable == null) return null;
            var provider = queryable.Provider as DefaultQueryProvider;
            if (provider == null) return null;

            var propertyInfo = typeof(DefaultQueryProvider).GetProperty("Session", BindingFlags.NonPublic | BindingFlags.Instance);
            var result = propertyInfo.GetValue(provider);

            return result as ISession;
        }
    }
}
