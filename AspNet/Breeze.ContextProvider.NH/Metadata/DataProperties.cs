using System.Collections.Generic;

namespace Breeze.ContextProvider.NH.Metadata
{
    public class DataProperties : MetadataList<DataProperty>
    {
        public DataProperties()
        {
        }

        public DataProperties(List<Dictionary<string, object>> listOfDict) : base(listOfDict)
        {
        }

        protected override DataProperty Convert(Dictionary<string, object> item)
        {
            return new DataProperty(item);
        }
    }
}
