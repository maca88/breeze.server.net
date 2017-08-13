using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NHibernate.Type;

namespace Breeze.ContextProvider.NH
{
    public class NHSyntheticProperty
    {
        public string Name { get; set; }

        public bool IsNullable { get; set; }

        public IType FkType { get; set; }

        public string FkPropertyName { get; set; }

        public IType PkType { get; set; }

        public string PkPropertyName { get; set; }
    }
}
