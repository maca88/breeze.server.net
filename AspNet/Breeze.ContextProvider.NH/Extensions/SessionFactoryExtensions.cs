using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate;
using NHibernate.Metadata;

namespace Breeze.ContextProvider.NH.Extensions
{
    public static class SessionFactoryExtensions
    {
        private static readonly Dictionary<ISessionFactory, Dictionary<Type, List<NHSyntheticProperty>>> SyntheticProperties =
                new Dictionary<ISessionFactory, Dictionary<Type, List<NHSyntheticProperty>>>();

        static SessionFactoryExtensions() { }

        public static void SetSyntheticProperties(this ISessionFactory sessionFactory, Dictionary<Type, List<NHSyntheticProperty>> dict)
        {
            SyntheticProperties[sessionFactory] = dict;
        }

        public static Dictionary<Type, List<NHSyntheticProperty>> GetSyntheticProperties(this ISessionFactory sessionFactory)
        {
            return SyntheticProperties.ContainsKey(sessionFactory)
                ? SyntheticProperties[sessionFactory]
                : null;
        }

        public static List<NHSyntheticProperty> GetSyntheticProperties(this IClassMetadata metadata)
        {
            if (metadata == null)
            {
                return null;
            }

            var type = metadata.GetMappedClass(EntityMode.Poco);
            var key = SyntheticProperties.Keys.FirstOrDefault(o => o.GetClassMetadata(type) == metadata);
            if (key == null || !SyntheticProperties.ContainsKey(key) || !SyntheticProperties[key].ContainsKey(type))
            {
                return null;
            }
            return SyntheticProperties[key][type];
        }

    }
}
