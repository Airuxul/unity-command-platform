namespace Air.UnityConnector.Invoke
{
    public sealed class InvokeResult
    {
        public string Code { get; set; } = "ok";
        public string Message { get; set; } = "ok";
        public object Payload { get; set; }

        public static InvokeResult Ok(string message = "ok", object payload = null) =>
            new()
            {
                Code = "ok",
                Message = string.IsNullOrWhiteSpace(message) ? "ok" : message,
                Payload = payload,
            };
    }
}
