namespace Air.UnityConnector.Params
{
    public sealed class EchoParams
    {
        [CliParam(Description = "text to echo")]
        public string Message { get; set; } = "ok";
    }
}
