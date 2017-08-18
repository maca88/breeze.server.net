using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using System.Text;

namespace Breeze.ContextProvider.NH
{
    public class JsonFormatter
    {
        public static JsonMediaTypeFormatter Create()
        {
            var jsonSerializerSettings = BreezeConfig.Instance.GetJsonSerializerSettings();

            var formatter = new JsonMediaTypeFormatter();
            formatter.SerializerSettings = jsonSerializerSettings;
            formatter.SupportedMediaTypes.Add(new MediaTypeHeaderValue("application/json"));
            formatter.SupportedEncodings.Add(new UTF8Encoding(false, true));

            return formatter;
        }
    }
}
