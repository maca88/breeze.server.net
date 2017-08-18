using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http.Filters;
using Newtonsoft.Json;

namespace Breeze.ContextProvider.NH.Core
{
    public class GlobalExceptionFilter : IExceptionFilter
    {
        public bool AllowMultiple { get; } = false;
        
        public Task ExecuteExceptionFilterAsync(HttpActionExecutedContext context, CancellationToken cancellationToken)
        {
            var ex = context.Exception;
            var msg = ex.InnerException == null ? ex.Message : ex.Message + "--" + ex.InnerException.Message;

            var statusCode = 500;
            var response = new ErrorDto()
            {
                Message = msg,
                StackTrace = context.Exception.StackTrace

            };

            var eeEx = ex as EntityErrorsException;
            if (eeEx != null)
            {
                response.Code = (int)eeEx.StatusCode;
                response.EntityErrors = eeEx.EntityErrors;
                statusCode = response.Code;
            }

            var json = JsonConvert.SerializeObject(response);
            var errorDto = JsonConvert.DeserializeObject<ErrorDto>(json);
            context.Response = context.Request.CreateResponse((HttpStatusCode)statusCode, JsonConvert.SerializeObject(errorDto));

            return null;
        }
    }

    public class ErrorDto
    {
        public int Code { get; set; }
        public string Message { get; set; }
        public string StackTrace { get; set; }
        public List<EntityError> EntityErrors { get; set; }

        // other fields

        public override string ToString()
        {
            return JsonConvert.SerializeObject(this);
        }
    }
}
