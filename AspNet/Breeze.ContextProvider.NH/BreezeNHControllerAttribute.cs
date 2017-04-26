using System.Net.Http;
using System.Net.Http.Formatting;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Dispatcher;
using Breeze.WebApi2;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Http.OData.Query;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using NHibernate;

namespace Breeze.ContextProvider.NH
{
    /// <summary>
    /// Configure the Web API settings for this Breeze Controller
    /// </summary>
    /// <remarks>
    /// Clears all <see cref="MediaTypeFormatter"/>s and 
    /// adds the Breeze formatter for JSON content.
    /// Removes the competing ASP.NET Web API's QueryFilterProvider if present. 
    /// Adds <see cref="EnableBreezeQueryFilterProvider"/> for OData query processing
    /// Adds <see cref="MetadataFilterProvider"/> returning a Metadata action filter
    /// which (by default) converts a Metadata string response to
    /// an HTTP response with string content.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class)]
    public class BreezeNHControllerAttribute : Attribute, IControllerConfiguration
    {

        /// <summary>
        /// Initialize the Breeze controller with a single <see cref="MediaTypeFormatter"/> for JSON
        /// and a single <see cref="IFilterProvider"/> for Breeze OData support
        /// </summary>
        public virtual void Initialize(HttpControllerSettings settings, HttpControllerDescriptor descriptor)
        {
            lock (__lock)
            {
                // Remove the Web API's "QueryFilterProvider" 
                // and any previously added EnableBreezeQueryFilterProvider.
                // Add the value from BreezeFilterProvider()
                settings.Services.RemoveAll(typeof(IFilterProvider),
                                            f => (f.GetType().Name == "QueryFilterProvider")
                                                 || (f is BreezeNHQueryableFilterProvider));
                settings.Services.Add(typeof(IFilterProvider), GetQueryableFilterProvider(_queryableFilter));
                settings.Services.Add(typeof(IFilterProvider), GetMetadataFilterProvider(_metadataFilter));
                settings.Services.Add(typeof(IFilterProvider), GetEntityErrorsFilterProvider(_entityErrorsFilter));

                // remove all formatters and add only the Breeze JsonFormatter
                settings.Formatters.Clear();
                settings.Formatters.Add(GetJsonFormatter(settings, descriptor.Configuration));
            }
        }

        /// <summary>
        /// Gets or sets max expansion depth at controller level
        /// </summary>
        public int MaxExpansionDepth
        {
            get { return _queryableFilter.MaxExpansionDepth; }
            set { _queryableFilter.MaxExpansionDepth = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating whether query composition should
        /// alter the original query when necessary to ensure a stable sort order.
        /// </summary>
        /// <value>A <c>true</c> value indicates the original query should
        /// be modified when necessary to guarantee a stable sort order.
        /// A <c>false</c> value indicates the sort order can be considered
        /// stable without modifying the query.  Query providers that ensure
        /// a stable sort order should set this value to <c>false</c>.
        /// The default value is <c>true</c>.</value>
        public bool EnsureStableOrdering
        {
            get { return _queryableFilter.EnsureStableOrdering; }
            set { _queryableFilter.EnsureStableOrdering = value; }
        }

        /// <summary>
        /// Gets or sets a value indicating how null propagation should
        /// be handled during query composition. 
        /// </summary>
        /// <value>
        /// The default is <see cref="HandleNullPropagationOption.Default"/>.
        /// </value>
        public HandleNullPropagationOption HandleNullPropagation
        {
            get { return _queryableFilter.HandleNullPropagation; }
            set { _queryableFilter.HandleNullPropagation = value; }
        }

        /// <summary>
        /// Gets or sets the maximum depth of the Any or All elements nested inside the query.
        /// </summary>
        /// <remarks>
        /// This limit helps prevent Denial of Service attacks. The default value is 1.
        /// </remarks>
        /// <value>
        /// The maxiumum depth of the Any or All elements nested inside the query.
        /// </value>
        public int MaxAnyAllExpressionDepth
        {
            get { return _queryableFilter.MaxAnyAllExpressionDepth; }
            set { _queryableFilter.MaxAnyAllExpressionDepth = value; }
        }

        /// <summary>
        /// Gets or sets the maximum number of query results to send back to clients.
        /// </summary>
        /// <value>
        /// The maximum number of query results to send back to clients.
        /// </value>
        public int PageSize
        {
            get { return _queryableFilter.PageSize; }
            set { _queryableFilter.PageSize = value; }
        }

        public AllowedQueryOptions AllowedQueryOptions
        {
            get { return _queryableFilter.AllowedQueryOptions; }
            set { _queryableFilter.AllowedQueryOptions = value; }
        }

        public AllowedFunctions AllowedFunctions
        {
            get { return _queryableFilter.AllowedFunctions; }
            set { _queryableFilter.AllowedFunctions = value; }
        }

        public AllowedArithmeticOperators AllowedArithmeticOperators
        {
            get { return _queryableFilter.AllowedArithmeticOperators; }
            set { _queryableFilter.AllowedArithmeticOperators = value; }
        }

        public AllowedLogicalOperators AllowedLogicalOperators
        {
            get { return _queryableFilter.AllowedLogicalOperators; }
            set { _queryableFilter.AllowedLogicalOperators = value; }
        }

        public string AllowedOrderByProperties
        {
            get { return _queryableFilter.AllowedOrderByProperties; }
            set { _queryableFilter.AllowedOrderByProperties = value; }
        }

        public int MaxSkip
        {
            get { return _queryableFilter.MaxSkip; }
            set { _queryableFilter.MaxSkip = value; }
        }

        public int MaxTop
        {
            get { return _queryableFilter.MaxTop; }
            set { _queryableFilter.MaxTop = value; }
        }


        /// <summary>
        /// Return the IQueryable <see cref="IFilterProvider"/> for a Breeze Controller
        /// </summary>
        /// <remarks>
        /// By default returns an <see cref="EnableBreezeQueryFilterProvider"/>.
        /// Override to substitute a custom provider.
        /// </remarks>
        protected virtual IFilterProvider GetQueryableFilterProvider(BreezeNHQueryableAttribute defaultFilter)
        {
            return new BreezeNHQueryableFilterProvider(defaultFilter);
        }

        /// <summary>
        /// Return the Breeze-specific <see cref="MediaTypeFormatter"/> that formats
        /// content to JSON. This formatter must be tailored to work with Breeze clients. 
        /// </summary>
        /// <remarks>
        /// By default returns the Breeze <see cref="JsonFormatter"/>.
        /// Override it to substitute a custom JSON formatter.
        /// </remarks>
        protected virtual MediaTypeFormatter GetJsonFormatter(HttpControllerSettings settings, HttpConfiguration httpConfiguration)
        {
            var formatter = JsonFormatter.Create();
            var jsonSerializer = formatter.SerializerSettings;
            if (!formatter.SerializerSettings.Converters.Any(o => o is NHibernateProxyJsonConverter))
                jsonSerializer.Converters.Add(new NHibernateProxyJsonConverter());
            jsonSerializer.ContractResolver = (IContractResolver)httpConfiguration.DependencyResolver.GetService(typeof(NHibernateContractResolver));
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

        protected BreezeNHQueryableAttribute _queryableFilter = new BreezeNHQueryableAttribute();
        private MetadataToHttpResponseAttribute _metadataFilter = new MetadataToHttpResponseAttribute();
        private EntityErrorsFilterAttribute _entityErrorsFilter = new EntityErrorsFilterAttribute();
        private static object __lock = new object();


        // These instances are stateless and threadsafe so can use static versions for all controller instances
        private static readonly MediaTypeFormatter DefaultJsonFormatter = JsonFormatter.Create();
    }

    internal class BreezeNHQueryableFilterProvider : IFilterProvider
    {

        public BreezeNHQueryableFilterProvider(BreezeNHQueryableAttribute filter)
        {
            _filter = filter;
        }

        public IEnumerable<FilterInfo> GetFilters(HttpConfiguration configuration, HttpActionDescriptor actionDescriptor)
        {
            if (actionDescriptor == null ||
              (!IsIQueryable(actionDescriptor.ReturnType)) ||
              actionDescriptor.GetCustomAttributes<EnableBreezeQueryAttribute>().Any() || // if method already has a QueryableAttribute (or subclass) then skip it.
              actionDescriptor.GetParameters().Any(parameter => typeof(ODataQueryOptions).IsAssignableFrom(parameter.ParameterType))
            )
            {
                return Enumerable.Empty<FilterInfo>();
            }

            return new[] { new FilterInfo(_filter, FilterScope.Global) };
        }

        internal static bool IsIQueryable(Type type)
        {
            if (type == typeof(IQueryable)) return true;
            if (type != null && type.IsGenericType)
            {
                return type.GetGenericTypeDefinition() == typeof(IQueryable<>);
            }
            return false;
        }

        private readonly BreezeNHQueryableAttribute _filter;
    }

    internal class MetadataFilterProvider : IFilterProvider
    {

        public MetadataFilterProvider(MetadataToHttpResponseAttribute filter)
        {
            _filter = filter;
        }

        public IEnumerable<FilterInfo> GetFilters(HttpConfiguration configuration, HttpActionDescriptor actionDescriptor)
        {
            return new[] { new FilterInfo(_filter, FilterScope.Controller) };
        }

        private readonly MetadataToHttpResponseAttribute _filter;
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
