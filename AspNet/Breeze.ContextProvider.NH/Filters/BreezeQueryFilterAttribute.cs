using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.Reflection;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Breeze.ContextProvider.NH.Core;
using Breeze.Core;
using Newtonsoft.Json.Serialization;
using NHibernate;
using NHibernate.Linq;
using QueryResult = Breeze.Core.QueryResult;

namespace Breeze.ContextProvider.NH.Filters
{
    [AttributeUsage(AttributeTargets.Class,  Inherited = false)]
    public class BreezeQueryFilterAttribute : ActionFilterAttribute, IControllerConfiguration
    {
        private MetadataToHttpResponseAttribute _metadataFilter = new MetadataToHttpResponseAttribute();
        private readonly EntityErrorsFilterAttribute _entityErrorsFilter = new EntityErrorsFilterAttribute();

        /// <summary>
        /// Initialize the Breeze controller with a single <see cref="MediaTypeFormatter"/> for JSON
        /// and a single <see cref="IFilterProvider"/> for Breeze OData support
        /// </summary>
        public virtual void Initialize(HttpControllerSettings settings, HttpControllerDescriptor descriptor)
        {
            settings.Services.Add(typeof(IFilterProvider), GetMetadataFilterProvider(_metadataFilter));
            settings.Services.Add(typeof(IFilterProvider), GetEntityErrorsFilterProvider(_entityErrorsFilter));

            // remove all formatters and add only the Breeze JsonFormatter
            settings.Formatters.Clear();
            settings.Formatters.Add(GetJsonFormatter(descriptor.Configuration));
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
                return;
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
                
                context.Response = context.Request.CreateResponse(HttpStatusCode.OK, qr);
            }
            
            base.OnActionExecuted(context);
        }

        /// <summary>
        /// Return the Metadata <see cref="IFilterProvider"/> for a Breeze Controller
        /// </summary>
        /// <remarks>
        /// By default returns an <see cref="MetadataToHttpResponseAttribute"/>.
        /// Override to substitute a custom provider.
        /// </remarks>
        protected virtual IFilterProvider GetMetadataFilterProvider(MetadataToHttpResponseAttribute metadataFilter)
        {
            return new MetadataFilterProvider(metadataFilter);
        }

        protected virtual IFilterProvider GetEntityErrorsFilterProvider(EntityErrorsFilterAttribute entityErrorsFilter)
        {
            return new EntityErrorsFilterProvider(entityErrorsFilter);
        }

        /// <summary>
        /// Return the Breeze-specific <see cref="MediaTypeFormatter"/> that formats
        /// content to JSON. This formatter must be tailored to work with Breeze clients. 
        /// </summary>
        /// <remarks>
        /// By default returns the Breeze <see cref="JsonFormatter"/>.
        /// Override it to substitute a custom JSON formatter.
        /// </remarks>
        protected virtual JsonMediaTypeFormatter GetJsonFormatter(HttpConfiguration configuration)
        {
            var formatter = JsonFormatter.Create();
            var jsonSerializer = formatter.SerializerSettings;
            if (!formatter.SerializerSettings.Converters.Any(o => o is NHibernateProxyJsonConverter))
                jsonSerializer.Converters.Add(new NHibernateProxyJsonConverter());
            jsonSerializer.ContractResolver = (IContractResolver)configuration.DependencyResolver.GetService(typeof(NHibernateContractResolver));
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
