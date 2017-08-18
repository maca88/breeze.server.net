using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Breeze.Core {
  public static class EntityQueryExtensions {
    private static readonly MethodInfo IncludeMethod;
    
    static EntityQueryExtensions()
    {
      IncludeMethod = AppDomain.CurrentDomain.GetAssemblies()
              .Where(o => !o.IsDynamic)
              .Where(o => o.GetName().Name == "NHibernate.Extensions")
              .Select(o => o.GetTypes().First(t => t.FullName == "NHibernate.Linq.LinqExtensions"))
              .Select(o => o.GetMethods().First(m => m.Name == "Include" && !m.IsGenericMethod && m.GetParameters().Length == 2))
              .FirstOrDefault();
    }
    
    public static IQueryable ApplyWhere(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.WherePredicate != null) {
        queryable = QueryBuilder.ApplyWhere(queryable, eleType, eq.WherePredicate);
      }
      return queryable;
    }

    public static IQueryable ApplyOrderBy(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.OrderByClause != null) {
        queryable = QueryBuilder.ApplyOrderBy(queryable, eleType, eq.OrderByClause);
      }
      return queryable;
    }

    public static IQueryable ApplySelect(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.SelectClause != null) {
        queryable = QueryBuilder.ApplySelect(queryable, eleType, eq.SelectClause);
      }
      return queryable;
    }

    public static IQueryable ApplySkip(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.SkipCount.HasValue) {
        queryable = QueryBuilder.ApplySkip(queryable, eleType, eq.SkipCount.Value);
      }
      return queryable;
    }

    public static IQueryable ApplyTake(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.TakeCount.HasValue) {
        queryable = QueryBuilder.ApplyTake(queryable, eleType, eq.TakeCount.Value);
      }
      return queryable;
    }

    public static IQueryable ApplyExpand(this EntityQuery eq, IQueryable queryable, Type eleType) {
      if (eq.ExpandClause != null && IncludeMethod != null) {
        eq.ExpandClause.PropertyPaths.ToList().ForEach(expand => {
          queryable = (IQueryable)IncludeMethod.Invoke(null, new object[] { queryable, expand.Replace("/", ".") });
        });
      }
      return queryable;
    }
  }
}