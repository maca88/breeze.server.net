using System.Net.Http;
using System.Web.Http.Filters;
using System.Web.Http.OData.Query.Validators;
using Breeze.WebApi2;
using Microsoft.Data.Edm;
using Microsoft.Data.OData.Query;
using Microsoft.Data.OData.Query.SemanticAst;
using NHibernate;
using NHibernate.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Formatting;
using System.Reflection;
using System.Threading.Tasks;
using System.Web.Http.OData.Query;

namespace Breeze.ContextProvider.NH
{
    public class NHQueryHelper : QueryHelper
    {
        protected string[] expandPaths;
        protected ISession session;
        private static readonly MethodInfo IncludeMethod;

        static NHQueryHelper()
        {
            IncludeMethod = AppDomain.CurrentDomain.GetAssemblies()
                .Where(o => !o.IsDynamic)
                .Where(o => o.GetName().Name == "NHibernate.Extensions")
                .Select(o => o.GetTypes().First(t => t.FullName == "NHibernate.Linq.LinqExtensions"))
                .Select(o => o.GetMethods().First(m => m.Name == "Include" && !m.IsGenericMethod && m.GetParameters().Length == 2))
                .FirstOrDefault();
        }

        public NHQueryHelper(bool enableConstantParameterization, bool ensureStableOrdering, HandleNullPropagationOption handleNullPropagation, int pageSize)
            : base(enableConstantParameterization, ensureStableOrdering, handleNullPropagation, pageSize)
        {
        }

        public NHQueryHelper(ODataQuerySettings querySettings) : base(querySettings)
        {
        }

        public NHQueryHelper() : base()
        {
        }

        // Controls whether we always handle expands (vs. letting WebApi take care of it)
        public override bool ManuallyExpand { get { return true; } }

        /// <summary>
        /// Before applying the queryOptions to the queryable, perform special processing to handle
        /// $expand and work around the NHibernate IQueryable limitations
        /// </summary>
        /// <param name="queryable"></param>
        /// <param name="queryOptions"></param>
        /// <returns></returns>
        public override IQueryable BeforeApplyQuery(IQueryable queryable, ODataQueryOptions queryOptions)
        {
            var nhQueryable = queryable as IQueryableInclude;
            if (nhQueryable != null)
            {
                queryable = NHApplyExpand(nhQueryable);
            }
            queryable = NHApplyExpand(queryable, queryOptions);

            return queryable;
        }

        /// <summary>
        /// Saves the expand path strings from queryable.GetIncludes(),
        /// for later lazy initialization and serialization.
        /// </summary>
        /// <param name="queryable"></param>
        /// <returns></returns>
        protected IQueryable NHApplyExpand(IQueryableInclude queryable)
        {
            var expands = queryable.GetIncludes();
            if (expands != null && expands.Count > 0)
            {
                this.expandPaths = expands.ToArray();
            }
            return queryable;
        }

        /// <summary>
        /// Saves the expand path string from the queryOptions, 
        /// for later lazy initialization and serialization.
        /// </summary>
        /// <param name="queryable"></param>
        /// <param name="expandsQueryString"></param>
        /// <returns></returns>
        protected IQueryable NHApplyExpand(IQueryable queryable, ODataQueryOptions queryOptions) {
            var expandQueryString = queryOptions.RawValues.Expand;
            if (string.IsNullOrWhiteSpace(expandQueryString)) return queryable;
            string[] expandPaths = expandQueryString.Split(',').Select(s => s.Trim()).ToArray();
            if (this.expandPaths != null)
            {
                this.expandPaths = this.expandPaths.Concat(expandPaths).ToArray();
            }
            else
            {
                this.expandPaths = expandPaths;
            }

            return queryable;
        }

        /// <summary>
        /// Override ApplyExpand to do nothing.  NHApplyExpand takes care of expands for NH, and 
        /// it is executed earlier in the query processing (in BeforeApplyQuery)
        /// </summary>
        /// <param name="queryable"></param>
        /// <param name="queryOptions"></param>
        /// <returns></returns>
        public override IQueryable ApplyExpand(IQueryable queryable, ODataQueryOptions queryOptions)
        {
            if (IncludeMethod == null || expandPaths == null)
            {
                return queryable;
            }
            foreach (var path in expandPaths)
            {
                IncludeMethod.Invoke(null, new object[] { queryable, path.Replace("/", ".") });
            }
            return queryable;
        }

        /// <summary>
        /// Get the ISession from the IQueryable.
        /// </summary>
        /// <param name="queryable"></param>
        /// <returns>the session if queryable.Provider is NHibernate.Linq.DefaultQueryProvider, else null</returns>
        public ISession GetSession(IQueryable queryable)
        {
            if (session != null) return session;
            if (queryable == null) return null;
            var provider = queryable.Provider as DefaultQueryProvider;
            if (provider == null) return null;

            var propertyInfo = typeof(DefaultQueryProvider).GetProperty("Session", BindingFlags.NonPublic | BindingFlags.Instance);
            var result = propertyInfo.GetValue(provider);
            var isession = result as ISession;
            if (isession != null) this.session = isession;
            return session;
        }

        /// <summary>
        /// Perform the lazy loading allowed in the expandPaths.
        /// </summary>
        /// <param name="list"></param>
        public override IEnumerable PostExecuteQuery(IEnumerable list)
        {
            if (expandPaths != null && IncludeMethod == null)
            {
                NHExpander.InitializeList(list, expandPaths);
            }
            return list;
        }

        /// <summary>
        /// Configure the JsonFormatter to limit the object serialization of the response.
        /// Even with no IQueryable, we still need to configure the formatter to prevent runaway serialization.
        /// We have to rely on the controller to close the session in this case.
        /// </summary>
        /// <param name="jsonFormatter"></param>
        /// <param name="queryable">Used to obtain the ISession</param>
        public override void ConfigureFormatter(JsonMediaTypeFormatter jsonFormatter, IQueryable queryable)
        {
            ConfigureFormatter(jsonFormatter, GetSession(queryable));
        }

        public override void ConfigureFormatter(HttpRequestMessage request, IQueryable queryable)
        {
            var jsonFormatter = request.GetConfiguration().Formatters.JsonFormatter;
            ConfigureFormatter(jsonFormatter, GetSession(queryable));
        }

        /// <summary>
        /// Configure the JsonFormatter to limit the object serialization of the response.
        /// </summary>
        /// <param name="jsonFormatter">request.GetConfiguration().Formatters.JsonFormatter</param>
        /// <param name="session">If not null, will be closed by this method.  Otherwise, the session must be closed by the Controller.</param>
        private void ConfigureFormatter(JsonMediaTypeFormatter jsonFormatter, ISession session)
        {
            var settings = jsonFormatter.SerializerSettings;
            ISessionFactory sessionFactory = null;

            if (session != null)
            {
                sessionFactory = session.SessionFactory;
                // Only serialize the properties that were initialized before session was closed
                Close();
            }
        }
#if ASYNC
        public virtual Task ConfigureFormatterAsync(HttpRequestMessage request, IQueryable queryable)
        {
            var jsonFormatter = request.GetConfiguration().Formatters.JsonFormatter;
            return ConfigureFormatterAsync(jsonFormatter, GetSession(queryable));
        }

        /// <summary>
        /// Configure the JsonFormatter to limit the object serialization of the response.
        /// </summary>
        /// <param name="jsonFormatter">request.GetConfiguration().Formatters.JsonFormatter</param>
        /// <param name="session">If not null, will be closed by this method.  Otherwise, the session must be closed by the Controller.</param>
        private async Task ConfigureFormatterAsync(JsonMediaTypeFormatter jsonFormatter, ISession session)
        {
            var settings = jsonFormatter.SerializerSettings;
            ISessionFactory sessionFactory = null;

            if (session != null)
            {
                sessionFactory = session.SessionFactory;
                // Only serialize the properties that were initialized before session was closed
                await CloseAsync();
            }
        }
#endif
        /// <summary>
        /// Release any resources associated with this QueryHelper.
        /// </summary>
        /// <param name="responseObject">Response payload, which may have associated resources.</param>
        public override void Close(object responseObject)
        {
            session = GetSession(responseObject as IQueryable);
            Close();
        }
#if ASYNC
        /// <summary>
        /// Release any resources associated with this QueryHelper.
        /// </summary>
        /// <param name="responseObject">Response payload, which may have associated resources.</param>
        public virtual Task CloseAsync(object responseObject)
        {
            session = GetSession(responseObject as IQueryable);
            return CloseAsync();
        }

        private async Task CloseAsync()
        {
            if (session == null || !session.IsOpen) return;
            if (session.GetSessionImplementation().TransactionInProgress)
            {
                var tx = session.Transaction;
                try
                {
                    if (tx.IsActive) await tx.CommitAsync();
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
#endif
        private void Close()
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
        /// Replaces the response.Content with the query results, wrapped in a QueryResult object if necessary.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="response"></param>
        /// <param name="queryResult"></param>
        public virtual async Task WrapResultAsync(HttpRequestMessage request, HttpResponseMessage response, IQueryable queryResult)
        {
            Object tmp;
            request.Properties.TryGetValue("MS_InlineCount", out tmp);
            var inlineCount = (Int64?)tmp;

            // if a select or expand was encountered we need to
            // execute the DbQueries here, so that any exceptions thrown can be properly returned.
            // if we wait to have the query executed within the serializer, some exceptions will not
            // serialize properly.

            var queryType = queryResult.GetType();

            if (!queryType.IsGenericType)
            {
                throw new ArgumentException("queryResult is not generic");
            }
            var entityType = queryType.GetGenericArguments().First();

            var listQueryResult = await ((dynamic)(typeof(LinqExtensionMethods).GetMethod("ToListAsync").MakeGenericMethod(entityType).Invoke(null, new object[] { queryResult })));

            var elementType = queryResult.ElementType;
            if (elementType.Name.StartsWith("SelectAllAndExpand"))
            {
                var prop = elementType.GetProperties().FirstOrDefault(pi => pi.Name == "Instance");
                var mi = prop.GetGetMethod();
                var lqr = (List<Object>)listQueryResult;
                listQueryResult = (dynamic)lqr.Select(item => {
                    var instance = mi.Invoke(item, null);
                    return (Object)instance;
                }).ToList();
            }

            // HierarchyNodeExpressionVisitor
            listQueryResult = PostExecuteQuery((IEnumerable)listQueryResult);

            if (listQueryResult != null || inlineCount.HasValue)
            {
                Object result = listQueryResult;
                if (inlineCount.HasValue)
                {
                    result = new QueryResult() { Results = listQueryResult, InlineCount = inlineCount };
                }

                var formatter = ((dynamic)response.Content).Formatter;
                var oc = new ObjectContent(result.GetType(), result, formatter);
                response.Content = oc;
            }
        }

    }
}
