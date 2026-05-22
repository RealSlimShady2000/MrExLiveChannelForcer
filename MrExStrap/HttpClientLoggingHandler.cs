using System.Net.Http;

namespace MrExStrap
{
    internal class HttpClientLoggingHandler : MessageProcessingHandler
    {
        public HttpClientLoggingHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        protected override HttpRequestMessage ProcessRequest(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Always log the basics. The verbose variant under DebugModeEnabled also dumps
            // request headers so users investigating "why is GitHub 403'ing me" can confirm
            // exactly what was sent over the wire.
            App.Logger.WriteLine("HttpClientLoggingHandler::ProcessRequest", $"{request.Method} {request.RequestUri}");

            if (App.Settings?.Prop?.DebugModeEnabled == true)
            {
                foreach (var header in request.Headers)
                    App.Logger.WriteLine("HttpClientLoggingHandler::RequestHeader", $"  {header.Key}: {string.Join(",", header.Value)}");
            }

            return request;
        }

        protected override HttpResponseMessage ProcessResponse(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            App.Logger.WriteLine("HttpClientLoggingHandler::ProcessResponse", $"{(int)response.StatusCode} {response.ReasonPhrase} {response.RequestMessage!.RequestUri}");

            if (App.Settings?.Prop?.DebugModeEnabled == true)
            {
                foreach (var header in response.Headers)
                    App.Logger.WriteLine("HttpClientLoggingHandler::ResponseHeader", $"  {header.Key}: {string.Join(",", header.Value)}");
                if (response.Content?.Headers != null)
                {
                    foreach (var header in response.Content.Headers)
                        App.Logger.WriteLine("HttpClientLoggingHandler::ResponseHeader", $"  {header.Key}: {string.Join(",", header.Value)}");
                }
            }

            return response;
        }
    }
}
