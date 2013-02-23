using Nancy;

namespace NancyTestWebsite
{
    public class HelloModule : NancyModule
    {
        public HelloModule()
        {
            Get["/"] = parameters => View["index"];
        }
    }
}














