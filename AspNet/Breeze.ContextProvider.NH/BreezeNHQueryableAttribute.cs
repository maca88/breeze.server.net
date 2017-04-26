using Breeze.WebApi2;
using System;
using System.Collections;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using System.Web.Http.OData;
using System.Web.Http.OData.Query;
using Microsoft.Data.Edm;

namespace Breeze.ContextProvider.NH {
    /// <summary>
    /// Override the EnableBreezeQueryAttribute to use NHQueryHelper, which applies OData syntax to NHibernate queries.
    /// Use this attribute on each method in your WebApi controller that uses Nhibernate's IQueryable.
    /// <see cref="http://www.breezejs.com/sites/all/apidocs/classes/EntityQuery.html#method_expand"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
    public class BreezeNHQueryableAttribute : EnableQueryAttribute
    {
        private static string QUERY_HELPER_KEY = "EnableBreezeQueryAttribute_QUERY_HELPER_KEY";
        private ODataValidationSettings _validationSettings;
        private static readonly MethodInfo DistinctMethodInfo;


        static BreezeNHQueryableAttribute()
        {
            DistinctMethodInfo =
                typeof(Queryable).GetMethods().First(o => o.Name == "Distinct" && o.GetParameters().Length == 1);
        }

        /// <summary>
        /// Sets HandleNullPropagation = false on the base class.  Otherwise it's true for non-EF, and that
        /// complicates the query expressions and breaks NH's query parser.
        /// </summary>
        public BreezeNHQueryableAttribute()
            : base()
        {
            // ensure EnableQueryAttribute supports Expand and Select by default because Breeze does.
            // Todo: confirm that this is still necessary as it was for predecessor QueryableAttribute
            this.AllowedQueryOptions = AllowedQueryOptions.Supported | AllowedQueryOptions.Expand | AllowedQueryOptions.Select;
            base.HandleNullPropagation = HandleNullPropagationOption.False;
            _validationSettings = (ODataValidationSettings)typeof(EnableQueryAttribute).GetField("_validationSettings", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(this);
        }

        /// <summary>
        /// Get the QueryHelper instance for the current request.  We use a single instance per request because
        /// QueryHelper is stateful, and may preserve state between the ApplyQuery and OnActionExecuted methods.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        protected QueryHelper GetQueryHelper(HttpRequestMessage request)
        {
            object qh;
            if (!request.Properties.TryGetValue(QUERY_HELPER_KEY, out qh))
            {
                qh = NewQueryHelper();
                request.Properties.Add(QUERY_HELPER_KEY, qh);
            }
            return (QueryHelper)qh;
        }

        public ODataQuerySettings GetODataQuerySettings()
        {
            var settings = new ODataQuerySettings
            {
                EnableConstantParameterization = this.EnableConstantParameterization,
                EnsureStableOrdering = this.EnsureStableOrdering,
                HandleNullPropagation = this.HandleNullPropagation,
                PageSize = this.PageSize > 0 ? this.PageSize : (int?)null
            };
            return settings;
        }

        protected virtual QueryHelper NewQueryHelper()
        {
            return new NHQueryHelper(GetODataQuerySettings());
        }

        public override async Task OnActionExecutedAsync(HttpActionExecutedContext actionExecutedContext, CancellationToken cancellationToken)
        {
            var response = actionExecutedContext.Response;
            if (response == null)
            {
                return;
            }

            object responseObject;
            if (!response.TryGetContentValue(out responseObject))
            {
                return;
            }

            var request = actionExecutedContext.Request;
            var actionDescriptor = actionExecutedContext.ActionContext.ActionDescriptor;
            var returnType = actionDescriptor.ReturnType;
            var queryHelper = GetQueryHelper(request) as NHQueryHelper;
            if (queryHelper == null)
            {
                throw new Exception("queryHelper must be a NHQueryHelper");
            }

            try
            {
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }
                if (typeof(IQueryable).IsAssignableFrom(returnType))
                {
                    var query = (IQueryable)responseObject;
                    if (query == null)
                    {
                        return;
                    }
                    var model = GetModel(query.ElementType, request, actionDescriptor);
                    if (model == null)
                        throw new InvalidOperationException("QueryGetModelMustNotReturnNull");
                    var queryOptions = new ODataQueryOptions(new ODataQueryContext(model, query.ElementType), request);
                    ValidateQuery(request, queryOptions);
                    query = ApplyQuery(query, queryOptions);
                    if (query == null)
                    {
                        return;
                    }
                    await queryHelper.WrapResultAsync(request, response, query);
                }
                // For non-IQueryable results, post-processing must be done manually by the developer.

                await queryHelper.ConfigureFormatterAsync(actionExecutedContext.Request, responseObject as IQueryable);
            }
            finally
            {
                await queryHelper.CloseAsync(responseObject);
            }
        }

        public override void ValidateQuery(HttpRequestMessage request, ODataQueryOptions queryOptions)
        {
            try
            {
                queryOptions.Validate(_validationSettings);
            }
            catch (Exception e)
            {
                // Ignore error if its message is like "Only properties specified in $expand can be traversed in $select query options"
                // because Breeze CAN support this by bypassing the OData processing.
                if (!(e.Message.Contains("$expand") && e.Message.Contains("$select")))
                {
                    throw; // any other error
                }
            }
        }

        /// <summary>
        /// All standard OData web api support is handled here (except select and expand).
        /// This method also handles nested orderby statements the the current ASP.NET web api does not yet support.
        /// This method is called by base.OnActionExecuted
        /// </summary>
        /// <param name="queryable"></param>
        /// <param name="queryOptions"></param>
        /// <returns></returns>
        public override IQueryable ApplyQuery(IQueryable queryable, ODataQueryOptions queryOptions)
        {
            var queryHelper = GetQueryHelper(queryOptions.Request);

            queryable = queryHelper.BeforeApplyQuery(queryable, queryOptions);
            queryable = queryHelper.ApplyQuery(queryable, queryOptions);
            var paramValues = queryOptions.Request.GetQueryNameValuePairs().ToDictionary(o => o.Key, o => o.Value);
            var qType = queryable.GetType();
            if (paramValues.ContainsKey("$distinct") && qType.IsGenericType)
            {
                queryable = (IQueryable)DistinctMethodInfo
                    .MakeGenericMethod(qType.GenericTypeArguments.First())
                    .Invoke(null, new[] { queryable });
            }
            return queryable;
        }
    }
}
