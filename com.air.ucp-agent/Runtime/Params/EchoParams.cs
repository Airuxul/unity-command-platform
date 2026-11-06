namespace Air.UcpAgent.Params
{
    public sealed class EchoParams
    {
        [UcpParam(Description = "text to echo")]
        public string Message { get; set; } = "ok";
    }
}
