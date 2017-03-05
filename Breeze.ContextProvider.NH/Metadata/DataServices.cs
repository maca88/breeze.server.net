using System.Collections.Generic;

namespace Breeze.ContextProvider.NH.Metadata
{
    public class DataServices : MetadataList<DataService>
    {
        public DataServices()
        {
        }

        public DataServices(List<Dictionary<string, object>> listOfDict) : base(listOfDict)
        {
        }

        protected override DataService Convert(Dictionary<string, object> item)
        {
            return new DataService(item);
        }
    }
}
