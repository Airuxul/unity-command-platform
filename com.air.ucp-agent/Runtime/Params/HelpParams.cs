namespace Air.UcpAgent.Params
{
    public sealed class HelpParams
    {
        public const string Json = "json";
        public const string Markdown = "md";

        [UcpParam(Description = "one command by name or alias: its card, with an example call and the error codes")]
        public string Command { get; set; }

        [UcpParam(Description = "md: the cards as markdown in data.markdown", AllowedValues = Json + "|" + Markdown)]
        public string Format { get; set; } = Json;
    }
}
