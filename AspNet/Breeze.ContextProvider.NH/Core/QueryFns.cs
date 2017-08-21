using System;
using System.Collections;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;

namespace Breeze.ContextProvider.NH.Core
{
    public static class QueryFns
    {
        public static IQueryable ExtractQueryable(HttpActionExecutedContext context)
        {
            object result;
            if (!context.Response.TryGetContentValue(out result))
            {
                return null;
            }

            IQueryable queryable;
            if (result is IQueryable)
            {
                queryable = (IQueryable)result;
            }
            else if (result is IEnumerable)
            {
                try
                {
                    queryable = ((IEnumerable)result).AsQueryable();
                }
                catch
                {
                    throw new Exception("Unable to convert this endpoints IEnumerable to an IQueryable. Try returning an IEnumerable<T> instead of just an IEnumerable.");
                }
            }
            else
            {
                return null;
            }

            return queryable;
        }
        
        public static string ExtractAndDecodeQueryString(HttpActionExecutedContext context)
        {
            var qs = context.Request.RequestUri.Query;
            var q = WebUtility.UrlDecode(qs);
            if (q.Length == 0)
            {
                return null;
            }
            var endIx = q.IndexOf('&');
            if (endIx > 1)
            {
                q = q.Substring(1, endIx - 1);
            }
            else
            {
                q = q.Substring(1);
            }
            if (q == "{}")
            {
                return null;
            }

            return q;
        }
    }
}
