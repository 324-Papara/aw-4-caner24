namespace Para.Api
{
    public class RequestProfilerModel
    {
        public string Request { get; set; }
        public string Response { get; set; }
        public HttpContext Context { get; set; }
        public DateTimeOffset RequestTime { get; set; }
        public DateTimeOffset ResponseTime { get; set; }


    }
}