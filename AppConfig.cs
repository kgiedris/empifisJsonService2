namespace empifisJsonAPI2
{
    public class AppConfig
    {
        public ServicePortConfig servicePort { get; set; }
        public JsonPathConfig JsonPathConfig { get; set; }
    }

    public class ServicePortConfig
    {
        public string port { get; set; }
        public string file_mode { get; set; }
        public string radison_error { get; set; }
        public int com_timeout_seconds { get; set; }
    }

    public class JsonPathConfig
    {
        public string InFilePath { get; set; }
        public string OutFilePath { get; set; }
    }
}