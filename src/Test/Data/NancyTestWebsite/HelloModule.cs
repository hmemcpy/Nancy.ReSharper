using Nancy;

namespace NancyTestWebsite
{
    public class MyModule : NancyModule
    {
        public MyModule() : base("/home")
        {
            Get["/lala"] = parameters => View["hi"];
        }
    }


    public class HelloModule : NancyModule
    {
        public HelloModule()
        {
            Get["/"] = parameters => View["index"];
        }
    }
}