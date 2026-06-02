namespace Air.UnityConnector.Http
{
    public sealed class HostAuthOptions
    {
        public string HeaderName { get; set; } = "X-Unity-Cmd-Token";
        public bool AcceptBearer { get; set; } = true;
    }
}
